namespace FSharp.OpenTTD.Admin.Actors

module Messages =

    open FSharp.OpenTTD.Admin.Networking.PacketTransformer
    open FSharp.OpenTTD.Admin.Models.State

    type Dispatcher =
        { PacketDispatcher : (PacketMessage -> unit) option
          StateDispatcher  : (GameState     -> unit) option }
    
    type Authorize =
        { Name    : string
          Pass    : string
          Version : string }

    type Message =
        | PacketReceivedMsg of PacketMessage
        | AuthorizeMsg      of Authorize