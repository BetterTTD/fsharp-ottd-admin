module Worker

open System.Net
open System.Threading.Tasks

open Bus

open FSharp.OpenTTD.Admin.Models.Configurations
open FSharp.OpenTTD.Admin.Actors.Messages
open FSharp.OpenTTD.Admin.OpenTTD

open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

let cfg =
    { Host = IPAddress.Parse("127.0.0.1")
      Port = 3977
      Pass = "12345"
      Name = "Local Serv"
      Tag  = "local-serv"
      Ver  = "1.0.0" }
    
type Worker(ottd : OpenTTD, bus : IBus, logger : ILogger<OpenTTD>) =
    let attachConnection cfg dispatcher =
        ottd.AttachConnection cfg dispatcher
        
    let removeConnection tag =
        ottd.RemoveConnection tag
        
    let defaultDispatcher (logger : ILogger) =
        { PacketDispatcher = Some (bus.Send "Packet")
          StateDispatcher  = Some (fun state  -> logger.LogDebug $"Dispatch: %A{state}" ) }
        
    interface IHostedService with
        member this.StartAsync _ =
            logger.LogInformation "Starting ..."
            
            let result = 
                let dispatcher = defaultDispatcher logger |> Some
                attachConnection cfg dispatcher
                |> Result.map (fun _ -> logger.LogInformation $"Connection attached: %A{cfg}")
            
            match result with
            | Ok _      -> logger.LogInformation "Work done successfully"
            | Error err -> logger.LogError err
            
            Task.CompletedTask
            
        member this.StopAsync _ =
            logger.LogInformation "Stopping ..."
            Task.CompletedTask
