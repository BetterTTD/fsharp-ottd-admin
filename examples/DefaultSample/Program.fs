open System.Net
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

open FSharp.OpenTTD.Admin.OpenTTD
open FSharp.OpenTTD.Admin.Models.Configurations
open FSharp.OpenTTD.Admin.Actors.Messages

let cfg =
    { Host = IPAddress.Parse("127.0.0.1")
      Port = 3977
      Pass = "12345"
      Name = "Local Serv"
      Tag  = "local-serv"
      Ver  = "1.0.0" }
    
let defaultDispatcher (logger : ILogger) =
    { PacketDispatcher = Some (fun packet -> logger.LogDebug $"Dispatch: %A{packet}")
      StateDispatcher  = Some (fun state  -> logger.LogDebug $"Dispatch: %A{state}" ) }
    
type Worker(logger : ILogger<OpenTTD>, ottd : OpenTTD) =
    let attachConnection cfg dispatcher =
        ottd.AttachConnection cfg dispatcher
        
    let removeConnection tag =
        ottd.RemoveConnection tag
    
    interface IHostedService with
        member this.StartAsync _ =
            logger.LogInformation "Starting ..."
            
            let result = 
                let dispatcher = defaultDispatcher logger |> Some
                attachConnection cfg dispatcher
                |> Result.map (fun _ -> logger.LogInformation $"Connection attached: %A{cfg}")
                |> Result.bind (fun _ -> removeConnection cfg.Tag)
                |> Result.map (fun _ -> logger.LogInformation $"Connection removed: %A{cfg.Tag}")
                |> Result.map (fun _ -> Thread.Sleep(2000))
                |> Result.bind (fun _ -> attachConnection cfg dispatcher)
                |> Result.map (fun _ -> logger.LogInformation $"Connection attached: %A{cfg}")
                |> Result.bind (fun _ -> removeConnection cfg.Tag)
                |> Result.map (fun _ -> logger.LogInformation $"Connection removed: %A{cfg.Tag}")
            
            match result with
            | Ok _      -> logger.LogInformation "Work done successfully"
            | Error err -> logger.LogError err
            
            Task.CompletedTask
            
        member this.StopAsync _ =
            logger.LogInformation "Stopping ..."
            Task.CompletedTask

let host argv =
    Host.CreateDefaultBuilder(argv)
        .ConfigureServices(fun services ->
            services
                .AddLogging()
                .AddTransient<OpenTTD>()
                .AddHostedService<Worker>()
            |> ignore)
        .ConfigureLogging(fun logger ->
            logger.AddConsole()
                  .AddDebug()
                  .SetMinimumLevel(LogLevel.Debug)
            |> ignore)
        .Build()

[<EntryPoint>]
let main argv =
    host(argv).Run()
    0