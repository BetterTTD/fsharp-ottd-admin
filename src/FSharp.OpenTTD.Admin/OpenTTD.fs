namespace FSharp.OpenTTD.Admin

open Akka.Actor
open Akka.Actor
open Akka.FSharp
open FSharp.OpenTTD.Admin.Actors
open FSharp.OpenTTD.Admin.Actors.Messages
open FSharp.OpenTTD.Admin.Models.Configurations
open Microsoft.Extensions.Logging

module OpenTTD =
    
    type OpenTTD(logger : ILogger<OpenTTD>) =

        let mutable actors = Map.empty

        let system = Configuration.defaultConfig() |> System.create "ottd-system"
        
        let coordinatorBuilder (cfg, dispatcher) =
            let coordinatorCfg = (cfg.Host, cfg.Port, cfg.Tag)
            Coordinator.init coordinatorCfg dispatcher
        
        member this.AttachConnection (cfg : ServerConfiguration) (dispatcher : Dispatcher option)  =
            if actors.ContainsKey(cfg.Tag) then
                Error $"Client already added for tag #{cfg.Tag}"
            else
                let ref = spawn system cfg.Tag <| coordinatorBuilder (cfg, dispatcher) 
                ref <! AuthorizeMsg { Name = cfg.Name; Pass = cfg.Pass; Version = cfg.Ver }
                actors <- actors.Add(cfg.Tag, ref)
                Ok ()
            
        member this.RemoveConnection (tag : string) =
            match actors.TryFind tag with
            | Some ref ->
                ref <! PoisonPill.Instance
                actors <- actors.Remove tag
                Ok ()
            | None -> Error $"Client was not found for tag #{tag}"