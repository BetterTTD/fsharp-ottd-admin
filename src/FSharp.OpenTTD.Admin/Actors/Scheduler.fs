namespace FSharp.OpenTTD.Admin.Actors

open System
open System.Timers
open Akka.Actor

module Scheduler =
        
    open Akka.FSharp

    type JobMessage = obj

    type Message =
        | AddJob of IActorRef * JobMessage * TimeSpan
        | PauseJob
        | ResumeJob
        
    let init (mailbox : Actor<Message>) =
        
        let timer = new Timer()
        timer.Start()
        mailbox.Defer (fun _ -> timer.Dispose())
        
        let rec loop () =
            
            actor {
                match! mailbox.Receive () with
                | AddJob (actor, msg, time) ->
                    // if job already exists, then skip somehow
                    timer.Interval <- time.TotalMilliseconds
                    timer.Elapsed.Add (fun _ -> actor <! msg) 
                    return! loop ()
                | PauseJob ->
                    timer.Stop ()
                    return! loop ()
                | ResumeJob ->
                    timer.Start ()
                    return! loop ()
            }
            
        loop ()