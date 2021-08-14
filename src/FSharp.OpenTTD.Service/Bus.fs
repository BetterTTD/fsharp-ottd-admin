module Bus

open System
open System.Text
open Newtonsoft.Json
open RabbitMQ.Client
open RabbitMQ.Client.Events

type IBus =
    abstract member Send:
        queue   : string ->
        msg     : 'a     -> unit
    abstract member Receive :
        queue   : string ->
        handler : ('a -> unit) -> unit

type RedditBus(channel : IModel) =
    interface IBus with
        member __.Receive queue handler =
            channel.QueueDeclare(
                queue      = queue,
                durable    = true,
                exclusive  = false,
                autoDelete = false)
            |> ignore
            
            let consumer = EventingBasicConsumer channel
            consumer.Received.AddHandler (fun _ ea ->
                Encoding.UTF8.GetString ea.Body.Span
                |> JsonConvert.DeserializeObject<'a>
                |> handler)
            
            channel.BasicConsume(
                queue    = queue,
                autoAck  = true,
                consumer = consumer)
            |> ignore
            
            channel.BasicConsume(queue, true, consumer) |> ignore
            
        member __.Send queue msg =
           channel.QueueDeclare(
               queue      = queue,
               durable    = true,
               exclusive  = false,
               autoDelete = false)
           |> ignore
           
           let props = channel.CreateBasicProperties()
           props.Persistent <- false
           
           let json = JsonConvert.SerializeObject(msg)
           let body = ReadOnlyMemory(Encoding.UTF8.GetBytes json)
           
           channel.BasicPublish(
               exchange        = "",
               routingKey      = queue,
               basicProperties = props,
               body            = body)

let createSimpleBus hostName =
    let connFact = ConnectionFactory(HostName = hostName)
    let conn = connFact.CreateConnection()
    let channel = conn.CreateModel()
    RedditBus channel

let createBus hostName port virtualHost username password =
    let connFact =
        ConnectionFactory(HostName    = hostName,
                          Port        = port,
                          VirtualHost = virtualHost,
                          UserName    = username,
                          Password    = password)
    let conn    = connFact.CreateConnection ()
    let channel = conn.CreateModel ()
    RedditBus channel
