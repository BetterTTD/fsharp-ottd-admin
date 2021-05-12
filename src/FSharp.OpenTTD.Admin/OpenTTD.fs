namespace FSharp.OpenTTD.Admin

open Akka.Actor
open Akka.FSharp
open FSharp.OpenTTD.Admin.Actors
open FSharp.OpenTTD.Admin.Actors.Messages
open FSharp.OpenTTD.Admin.Models.Configurations

module OpenTTD =
    
    type OpenTTD() =

        let mutable actors = Map.empty

        let system = Configuration.defaultConfig() |> System.create "ottd-system"
        
        member this.AttachConnection (cfg : ServerConfiguration) (dispatcher : Dispatcher option)  =
            if actors.ContainsKey(cfg.Tag) then
                Error $"Client already added for tag #{cfg.Tag}"
            else
                let coordinatorCfg = (cfg.Host, cfg.Port, cfg.Tag)
                let ref = spawn system cfg.Tag (Coordinator.init coordinatorCfg dispatcher)
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