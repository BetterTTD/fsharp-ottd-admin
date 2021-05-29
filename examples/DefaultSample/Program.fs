open System.Net
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
      Tag  = "local"
      Ver  = "1.0.0" }
    
let defaultDispatcher (logger : ILogger) =
    { PacketDispatcher = Some (fun packet -> logger.LogDebug $"Dispatch: %A{packet}")
      StateDispatcher  = Some (fun state  -> logger.LogDebug $"Dispatch: %A{state}" ) }
    
type Worker(logger : ILogger<OpenTTD>, ottd : OpenTTD) =
    interface IHostedService with
        member this.StartAsync(cts) =
            logger.LogInformation "Starting ..."
            let dispatcher = defaultDispatcher logger |> Some
            match ottd.AttachConnection cfg dispatcher with
            | Ok _      -> logger.LogInformation $"Connection attached: %A{cfg}"
            | Error err -> logger.LogError err
            
            Task.CompletedTask
        member this.StopAsync(cts) =
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