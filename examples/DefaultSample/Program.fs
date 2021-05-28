open FSharp.OpenTTD.Admin.OpenTTD
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

type Worker(logger : ILogger, ottd : OpenTTD) =
    interface IHostedService with
        member this.StartAsync(cts) =
            failwith "todo"
        member this.StopAsync(cts) =
            failwith "todo"

let builder argv =
    Host.CreateDefaultBuilder(argv)
        .ConfigureServices(fun services ->
            services
                .AddTransient<OpenTTD>()
                .AddHostedService<Worker>()
            |> ignore)
        .Build()

[<EntryPoint>]
let main argv =
    builder(argv).Run()
    0