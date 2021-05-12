namespace FSharp.OpenTTD.Admin.Actors

open System.IO

module Sender =

    open FSharp.OpenTTD.Admin.Networking.MessageTransformer
    open FSharp.OpenTTD.Admin.Networking.Packet

    open Akka.FSharp
     
    let init (stream : Stream) (mailbox : Actor<AdminMessage>) =
        let rec loop () =
            actor {
                let! msg = mailbox.Receive ()
                let { Buffer = buf; Size = size; } = msg |> msgToPacket |> prepareToSend
                stream.Write (buf, 0, int size)
                return! loop ()
            }
            
        loop ()
        