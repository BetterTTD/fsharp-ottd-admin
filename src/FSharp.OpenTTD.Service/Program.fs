
open Bus
open FSharp.OpenTTD.Admin.OpenTTD

open Giraffe

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting

let webApp =
    choose [
        route    "/ping"  >=> text "pong"
        RequestErrors.NOT_FOUND "Not Found"
    ]

let configureApp (app : IApplicationBuilder) =
    app.UseGiraffe webApp

let configureServices (services : IServiceCollection) =
    services.AddGiraffe() |> ignore
    services.AddLogging() |> ignore
    services.AddSingleton<IBus, RedditBus>(fun sp -> createSimpleBus "localhost") |> ignore
    services.AddTransient<OpenTTD>() |> ignore
    services.AddHostedService<Worker>() |> ignore

[<EntryPoint>]
let main _ =
    Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .Configure(configureApp)
                    .ConfigureServices(configureServices)
                    |> ignore)
        .Build()
        .Run()
    0