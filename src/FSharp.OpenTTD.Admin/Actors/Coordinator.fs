namespace FSharp.OpenTTD.Admin.Actors

open System
open System.Net
open System.Net.Sockets
open Akka.Event
open Microsoft.Extensions.Logging

module Coordinator =

    open FSharp.OpenTTD.Admin.Models
    open FSharp.OpenTTD.Admin.Actors.Messages
    open FSharp.OpenTTD.Admin.Networking.MessageTransformer
    open FSharp.OpenTTD.Admin.Networking.Enums
    open FSharp.OpenTTD.Admin.Networking.PacketTransformer

    open Akka.Actor
    open Akka.FSharp

    let private schedule (mailbox : Actor<_>) ref interval msg cancelKey =
        mailbox.Context.System.Scheduler.ScheduleTellRepeatedly(
            TimeSpan.FromMilliseconds 0.,
            TimeSpan.FromMilliseconds interval,
            ref, msg, mailbox.Self, cancelKey)
        
    let private connectToStream (ipAddress : IPAddress) (port : int) =
        let tcpClient = new TcpClient ()
        tcpClient.Connect (ipAddress, port)
        tcpClient.GetStream ()

    let private defaultPolls =
        [ { UpdateType = AdminUpdateType.ADMIN_UPDATE_COMPANY_INFO
            Data       = uint32 0xFFFFFFFF }
          { UpdateType = AdminUpdateType.ADMIN_UPDATE_CLIENT_INFO
            Data       = uint32 0xFFFFFFFF } ]
        |> List.map AdminPollMsg

    let private defaultUpdateFrequencies =
        [ { UpdateType = AdminUpdateType.ADMIN_UPDATE_CHAT
            Frequency  = AdminUpdateFrequency.ADMIN_FREQUENCY_AUTOMATIC }
          { UpdateType = AdminUpdateType.ADMIN_UPDATE_CLIENT_INFO
            Frequency  = AdminUpdateFrequency.ADMIN_FREQUENCY_AUTOMATIC }
          { UpdateType = AdminUpdateType.ADMIN_UPDATE_COMPANY_INFO
            Frequency  = AdminUpdateFrequency.ADMIN_FREQUENCY_AUTOMATIC } ]
        |> List.map AdminUpdateFreqMsg

    let private dispatchCore (dispatcher : Dispatcher option) (msg : PacketMessage, state : State.State) =
        match dispatcher with
        | Some dispatcher ->
            dispatcher.PacketDispatcher |> Option.iter (fun dispatch -> dispatch msg)
            dispatcher.StateDispatcher  |> Option.iter (fun dispatch -> dispatch state)
        | None -> ()
    
    let init (logger : ILogger) (host : IPAddress, port : int, tag : string) (dispatcher : Dispatcher option) (mailbox : Actor<Message>) =

        let dispatch    = dispatchCore dispatcher
        let cancelKey   = new Cancelable(mailbox.Context.System.Scheduler)
        let state       = State.init
        let stream      = connectToStream host port
        let senderRef   = Sender.init   stream |> spawn mailbox "sender"
        let receiverRef = Receiver.init stream |> spawn mailbox "receiver"
        
        mailbox.Defer (fun () ->
            cancelKey.Cancel()
            senderRef   <! PoisonPill.Instance
            receiverRef <! PoisonPill.Instance)
        
        let rec errored sender receiver state =
            actor {
                return! errored sender receiver state
            }
            
        and connected sender receiver state =
            actor {
                match! mailbox.Receive () with
                | PacketReceivedMsg msg ->
                    let state = State.dispatch state msg
                    dispatch (msg, state)
                    match msg with
                    | ServerChatMsg _ ->
                        match state.ChatHistory |> List.tryLast with
                        | Some chatAction -> printfn $"%A{chatAction}"
                        | None -> ()
                    | _ -> ()
                    return! connected sender receiver state
                | _ -> return UnhandledMessage
            }
            
        and connecting sender receiver state =
            actor {
                match! mailbox.Receive () with
                | PacketReceivedMsg msg ->
                    let state = State.dispatch state msg
                    dispatch (msg, state)
                    match msg with
                    | ServerProtocolMsg _ ->
                        defaultPolls @ defaultUpdateFrequencies |> List.iter (fun msg -> sender <! msg)
                        return! connecting sender receiver state
                    | ServerWelcomeMsg _ ->
                        return! connected sender receiver state
                    | _ ->
                        logger.LogError $"INVALID CONNECTING STATE CAPTURED FOR PACKET: %A{msg}"
                        return UnhandledMessage
                | _ -> return UnhandledMessage
            }
        
        and idle sender receiver state =
            actor {
                match! mailbox.Receive () with
                | AuthorizeMsg { Pass = pass; Name = name; Version = ver } ->
                    sender <! AdminJoinMsg { Password = pass; AdminName = name; AdminVersion = ver }
                    schedule mailbox receiver 1.0 "receive" cancelKey
                    return! connecting sender receiver state
                | _ -> return UnhandledMessage
            }
            
        idle senderRef receiverRef state