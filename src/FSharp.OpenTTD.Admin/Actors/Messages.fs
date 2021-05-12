namespace FSharp.OpenTTD.Admin.Actors

module Messages =

    open FSharp.OpenTTD.Admin.Networking.PacketTransformer

    type Authorize =
        { Name    : string
          Pass    : string
          Version : string }

    type Message =
        | PacketReceivedMsg of PacketMessage
        | AuthorizeMsg of Authorize