namespace FSharp.OpenTTD.Admin.Actors

open System
open System.Net
open System.Net.Sockets

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

    let init (host : IPAddress, port : int, tag : string) (mailbox : Actor<Message>) =

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
                    match msg with
                    | ServerChatMsg _ ->
                        match state.ChatHistory |> List.tryLast with
                        | Some chatAction -> printfn $"%A{chatAction}"
                        | None -> ()
                    | _ -> ()
                    return! connected sender receiver state
                | _ -> failwith "INVALID CONNECTING STATE CAPTURED"
            }
            
        and connecting sender receiver state =
            actor {
                match! mailbox.Receive () with
                | PacketReceivedMsg msg ->
                    let state = State.dispatch state msg
                    match msg with
                    | ServerProtocolMsg _ ->
                        defaultPolls @ defaultUpdateFrequencies |> List.iter (fun msg -> sender <! msg)
                        return! connecting sender receiver state
                    | ServerWelcomeMsg _ ->
                        return! connected sender receiver state
                    | _ -> failwithf $"INVALID CONNECTING STATE CAPTURED FOR PACKET: %A{msg}"
                | _ -> failwith "INVALID CONNECTING STATE CAPTURED"
            }
        
        and idle sender receiver state =
            actor {
                match! mailbox.Receive () with
                | AuthorizeMsg { Pass = pass; Name = name; Version = ver } ->
                    sender <! AdminJoinMsg { Password = pass; AdminName = name; AdminVersion = ver }
                    schedule mailbox receiver 1.0 "receive" cancelKey
                    return! connecting sender receiver state
                | _ -> failwith "INVALID IDLE STATE CAPTURED"
            }
            
        idle senderRef receiverRef state