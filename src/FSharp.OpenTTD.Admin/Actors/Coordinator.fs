﻿namespace FSharp.OpenTTD.Admin.Actors

module Coordinator =

    open System
    open System.Net
    open System.Net.Sockets

    open FSharp.OpenTTD.Admin.Actors
    open FSharp.OpenTTD.Admin.Actors.Messages
    open FSharp.OpenTTD.Admin.Models
    open FSharp.OpenTTD.Admin.Networking.PacketTransformer
    open FSharp.OpenTTD.Admin.Networking.MessageTransformer
    open FSharp.OpenTTD.Admin.Networking.Enums

    open Akka.Event
    open Akka.Actor
    open Akka.FSharp

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

    let private dispatchCore (dispatcher : Dispatcher option) (msg : PacketMessage, state : State.GameState) =
        match dispatcher with
        | Some dispatcher ->
            dispatcher.PacketDispatcher |> Option.iter (fun dispatch -> dispatch msg)
            dispatcher.StateDispatcher  |> Option.iter (fun dispatch -> dispatch state)
        | None -> ()
    
    let init (host : IPAddress, port : int, tag : string) (dispatcher : Dispatcher option) (mailbox : Actor<Message>) =

        let dispatch  = dispatchCore dispatcher
        let state     = State.init
        let stream    = connectToStream host port
        
        let senderRef     = Sender.init   stream |> spawn mailbox "sender"
        let receiverRef   = Receiver.init stream |> spawn mailbox "receiver"
        let schedulerRef  = Scheduler.init       |> spawn mailbox "scheduler"
        
        mailbox.Defer (fun () ->
            schedulerRef <! PoisonPill.Instance
            senderRef    <! PoisonPill.Instance
            receiverRef  <! PoisonPill.Instance
            stream.Dispose ())
        
        let rec errored sender receiver scheduler state =
            actor {
                scheduler <! Scheduler.PauseJob
                return! errored sender receiver scheduler state
            }
            
        and connected sender receiver scheduler state =
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
                    return! connected sender receiver scheduler state
                | _ -> return UnhandledMessage
            }
            
        and connecting sender receiver scheduler state =
            actor {
                match! mailbox.Receive () with
                | PacketReceivedMsg msg ->
                    let state = State.dispatch state msg
                    dispatch (msg, state)
                    match msg with
                    | ServerProtocolMsg _ ->
                        defaultPolls @ defaultUpdateFrequencies |> List.iter (fun msg -> sender <! msg)
                        return! connecting sender receiver scheduler state
                    | ServerWelcomeMsg _ ->
                        return! connected sender receiver scheduler state
                    | _ ->
                        return UnhandledMessage
                | _ -> return UnhandledMessage
            }
        
        and idle sender receiver scheduler state =
            actor {
                match! mailbox.Receive () with
                | AuthorizeMsg { Pass = pass; Name = name; Version = ver } ->
                    sender    <! AdminJoinMsg { Password = pass; AdminName = name; AdminVersion = ver }
                    scheduler <! Scheduler.AddJob (receiver, "receive", TimeSpan.FromSeconds(1.0))
                    return! connecting sender receiver scheduler state
                | _ -> return UnhandledMessage
            }
            
        idle senderRef receiverRef schedulerRef state