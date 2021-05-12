namespace FSharp.OpenTTD.Admin.Models

module State =

    open FSharp.OpenTTD.Admin.Networking.Enums
    open FSharp.OpenTTD.Admin.Networking.PacketTransformer
    
    type Company =
        { Id          : byte
          Name        : string
          ManagerName : string
          Color       : Color
          HasPassword : bool }

    type Client =
        { Id        : uint32
          Company   : Company
          Name      : string
          Host      : string
          Language  : NetworkLanguage }

    type GameInfo =
        { ServerName      : string
          NetworkRevision : string
          IsDedicated     : bool
          Landscape       : Landscape
          MapWidth        : int
          MapHeight       : int }

    type ChatAction =
        { Id              : int64
          Client          : Client option
          NetworkAction   : NetworkAction
          Destination     : ChatDestination
          Message         : string
          CompiledMessage : string }

    type State =
        { GameInfo    : GameInfo   option
          Clients     : Client     list
          Companies   : Company    list
          ChatHistory : ChatAction list }
        
    let init =
        let spectator =
            { Id          = byte 255
              Name        = "Spectator"
              ManagerName = ""
              Color       = Color.END
              HasPassword = false }
        { GameInfo = None; Clients = []; Companies = [ spectator ]; ChatHistory = [] }
        
    let dispatch (state : State) (msg : PacketMessage) =
        match msg with
        | ServerProtocolMsg _ -> state

        | ServerWelcomeMsg msg ->
            let gameInfo =
                { ServerName      = msg.ServerName
                  NetworkRevision = msg.NetworkRevision
                  IsDedicated     = msg.IsDedicated
                  Landscape       = msg.Landscape
                  MapWidth        = msg.MapWidth
                  MapHeight       = msg.MapHeight }
            { state with GameInfo = Some gameInfo} 

        | ServerChatMsg msg ->
            let client = state.Clients |> List.tryFind (fun cli -> cli.Id = msg.ClientId)
            let msgId =
                match state.ChatHistory with
                | [] -> 0L
                | actions -> actions |> List.maxBy (fun msg -> msg.Id) |> fun msg -> msg.Id + 1L
            
            match msg.NetworkAction with
            
            | NetworkAction.NETWORK_ACTION_SERVER_MESSAGE ->
                let action =
                    { Id = msgId
                      Client = None
                      NetworkAction = msg.NetworkAction
                      Destination = msg.ChatDestination
                      Message = msg.Message
                      CompiledMessage = msg.Message }
                { state with ChatHistory = state.ChatHistory @ [action] }
                
            | NetworkAction.NETWORK_ACTION_CHAT ->
                match client with
                | Some client ->
                    let action =
                        { Id = msgId
                          Client = Some client
                          NetworkAction = msg.NetworkAction
                          Destination = msg.ChatDestination
                          Message = msg.Message
                          CompiledMessage = $"<{client.Name}> {msg.Message}" }
                    { state with ChatHistory = state.ChatHistory @ [action] }
                | None -> state
                
            | NetworkAction.NETWORK_ACTION_CHAT_COMPANY -> 
                match client with
                | Some client ->
                    let action =
                        { Id = msgId
                          Client = Some client
                          NetworkAction = msg.NetworkAction
                          Destination = msg.ChatDestination
                          Message = msg.Message
                          CompiledMessage = $"<{client.Name}> {msg.Message}" }
                    { state with ChatHistory = state.ChatHistory @ [action] }
                | None -> state
                
            | NetworkAction.NETWORK_ACTION_CHAT_CLIENT -> 
                match client with
                | Some client ->
                    let action =
                        { Id = msgId
                          Client = Some client
                          NetworkAction = msg.NetworkAction
                          Destination = msg.ChatDestination
                          Message = msg.Message
                          CompiledMessage = $"<{client.Name}> {msg.Message}" }
                    { state with ChatHistory = state.ChatHistory @ [action] }
                | None -> state
                
            | NetworkAction.NETWORK_ACTION_COMPANY_SPECTATOR ->
                match client with
                | Some client ->
                    let action =
                        { Id = msgId
                          Client = Some client
                          NetworkAction = msg.NetworkAction
                          Destination = msg.ChatDestination
                          Message = msg.Message
                          CompiledMessage = $"{client.Name} is now spectating" }
                    { state with ChatHistory = state.ChatHistory @ [action] }
                | None -> state
                
            | NetworkAction.NETWORK_ACTION_COMPANY_JOIN ->
                match client with
                | Some client ->
                    let action =
                        { Id = msgId
                          Client = Some client
                          NetworkAction = msg.NetworkAction
                          Destination = msg.ChatDestination
                          Message = msg.Message
                          CompiledMessage = $"{client.Name} has joined company #{client.Company.Id + byte 1} ({client.Company.Name})" }
                    { state with ChatHistory = state.ChatHistory @ [action] }
                | None -> state
                
            | NetworkAction.NETWORK_ACTION_COMPANY_NEW ->
                match client with
                | Some client ->
                    let action =
                        { Id = msgId
                          Client = Some client
                          NetworkAction = msg.NetworkAction
                          Destination = msg.ChatDestination
                          Message = msg.Message
                          CompiledMessage = $"{client.Name} has started a new company (#{client.Company.Id + byte 1})" }
                    { state with ChatHistory = state.ChatHistory @ [action] }
                | None -> state

            | _ -> state

        | ServerClientJoinMsg _ -> state

        | ServerClientInfoMsg msg ->
            match state.Companies |> List.tryFind (fun cmp -> cmp.Id = msg.CompanyId) with
            | Some company -> 
                let client =
                    { Id       = msg.ClientId
                      Company  = company
                      Name     = msg.Name
                      Host     = msg.Address
                      Language = msg.Language }
                printfn $"%A{client}"
                let clients = state.Clients |> List.filter (fun cli -> cli.Id <> client.Id)
                { state with Clients = clients @ [ client ] }
            | None -> state

        | ServerClientUpdateMsg msg ->
            match state.Clients |> List.tryFind (fun cli -> cli.Id = msg.ClientId),
                  state.Companies |> List.tryFind (fun cmp -> cmp.Id = msg.CompanyId) with
            | Some client, Some company ->
                let client  = { client with Name = client.Name; Company = company }
                let clients = state.Clients |> List.filter (fun cli -> cli.Id <> client.Id)
                { state with Clients = clients @ [ client ] }
            | _ -> state

        | ServerClientQuitMsg msg ->
            let clients = state.Clients |> List.filter (fun cli -> cli.Id <> msg.ClientId)
            { state with Clients = clients }

        | ServerClientErrorMsg msg ->
            let clients = state.Clients |> List.filter (fun cli -> cli.Id <> msg.ClientId)
            { state with Clients = clients }
        
        | ServerCompanyNewMsg msg ->
            let company =
                { Id = msg.CompanyId
                  Name = "Unknown"
                  ManagerName = "Unknown"
                  Color = Color.END
                  HasPassword = false }
            let companies = state.Companies |> List.filter (fun cmp -> cmp.Id <> company.Id)
            { state with Companies = companies @ [ company ] }
        
        | ServerCompanyInfoMsg msg ->
            let company =
                { Id = msg.CompanyId
                  Name = msg.CompanyName
                  ManagerName = msg.ManagerName
                  Color = msg.Color
                  HasPassword = msg.HasPassword }
            let companies = state.Companies |> List.filter (fun cmp -> cmp.Id <> company.Id)
            { state with Companies = companies @ [ company ] }
        
        | ServerCompanyUpdateMsg msg ->
            match state.Companies |> List.tryFind (fun cmp -> cmp.Id = msg.CompanyId) with
            | Some company ->
                let company =
                    { company with Name  = msg.CompanyName; ManagerName = msg.CompanyName
                                   Color = msg.Color;       HasPassword = msg.HasPassword }
                let companies = state.Companies |> List.filter (fun cmp -> cmp.Id <> company.Id)
                { state with Companies = companies @ [ company ] }
            | None -> state
            
        | ServerCompanyRemoveMsg msg ->
            let companies = state.Companies |> List.filter (fun cmp -> cmp.Id <> msg.CompanyId)
            { state with Companies = companies }
