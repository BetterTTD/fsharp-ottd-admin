module Scheduler

    open System
    open Akka.Actor
    open Akka.FSharp
    open Akka.Util.Internal
    open Quartz
    open Quartz.Impl

    type JobMessage = obj
    type JobId      = obj

    type JobSchedule = 
        | Once                 of DateTimeOffset
        | RepeatForever        of TimeSpan
        | RepeatForeverAfter   of DateTimeOffset * TimeSpan
        | RepeatWithCount      of TimeSpan       * int
        | RepeatWithCountAfter of DateTimeOffset * TimeSpan * int

    type JobCommand =
        | CreateJob of IActorRef * JobMessage * JobSchedule
        | RemoveJob of JobId

    type JobCommandResult =
        | Success of JobId
        | Error   of JobId * Exception

    type private QuartzJob () =

        static let MessageKey = "message"
        static let ActorKey = "actor"

        interface IJob with
            member this.Execute (context : IJobExecutionContext) =
                let jdm = context.JobDetail.JobDataMap
                if jdm.ContainsKey(MessageKey) && jdm.ContainsKey(ActorKey) then
                    match jdm.[ActorKey] with
                    | :? IActorRef as actor -> actor <! jdm.[MessageKey]
                    | _ -> ()

        static member CreateBuilderWithData (actorRef : IActorRef, message : obj) =
            let jdm = JobDataMap()
            jdm.AddAndReturn(MessageKey, message).Add(ActorKey, actorRef)
            JobBuilder.Create<QuartzJob>().UsingJobData(jdm)

    let scheduleActor props (mailbox : Actor<_>) =

        let scheduler =
            match props with
            | Some props -> StdSchedulerFactory(props).GetScheduler()
            | None -> StdSchedulerFactory().GetScheduler()

        scheduler.Start()
        mailbox.Defer (fun _ -> scheduler.Shutdown())

        let createTriggerBuilder (jobSchedule : JobSchedule) =
            let builder = TriggerBuilder.Create()
            match jobSchedule with
            | Once startTime ->
                builder.StartAt(startTime)
            | RepeatForever interval ->
                builder.StartNow().WithSimpleSchedule(
                    fun x -> x.WithInterval(interval).RepeatForever() |> ignore)
            | RepeatForeverAfter (startTime, interval) ->
                builder.StartAt(startTime).WithSimpleSchedule(
                    fun x -> x.WithInterval(interval).RepeatForever() |> ignore)
            | RepeatWithCount (interval, count) ->                    
                builder.StartNow().WithSimpleSchedule(
                    fun x -> x.WithInterval(interval).WithRepeatCount(count) |> ignore)
            | RepeatWithCountAfter (startTime, interval, count) ->                    
                builder.StartAt(startTime).WithSimpleSchedule(
                    fun x -> x.WithInterval(interval).WithRepeatCount(count) |> ignore)

        let rec loop () =
            actor {
                let! message = mailbox.Receive ()
                match message with
                | CreateJob (actor, message, jobSchedule) ->
                    match (actor, jobSchedule) with
                    | null, _ -> mailbox.Sender() <! Error (null, ArgumentNullException("CreateJob actor is null"))
                    | _ -> 
                        let builder = createTriggerBuilder jobSchedule
                        let trigger = builder.Build()
                        try
                            let job = QuartzJob.CreateBuilderWithData(actor, message)
                                        .WithIdentity(trigger.JobKey)
                                        .Build()
                            scheduler.ScheduleJob(job, trigger) |> ignore
                            mailbox.Sender() <! Success trigger.JobKey
                        with ex ->
                            mailbox.Sender() <! Error (trigger.JobKey, ex)

                | RemoveJob jobKey ->
                    try
                        match scheduler.DeleteJob(jobKey :?> JobKey) with
                        | true ->
                            mailbox.Sender() <! Success jobKey
                        | false ->
                            mailbox.Sender() <! Error (jobKey, InvalidOperationException("Job not found"))
                    with ex ->
                        mailbox.Sender() <! Error (jobKey, ex)

                return! loop ()
            }

        loop ()