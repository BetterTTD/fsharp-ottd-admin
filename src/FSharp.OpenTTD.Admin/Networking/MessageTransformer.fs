﻿namespace FSharp.OpenTTD.Admin.Networking

module MessageTransformer =

    open Enums
    open Packet

    type AdminJoinMessage =
        { Password     : string
          AdminName    : string
          AdminVersion : string }

    type AdminUpdateFreqMessage =
        { UpdateType   : AdminUpdateType
          Frequency    : AdminUpdateFrequency }

    type AdminPollMessage =
        { UpdateType : AdminUpdateType
          Data       : uint32 }

    type AdminMessage =
        | AdminJoinMsg of AdminJoinMessage
        | AdminUpdateFreqMsg of AdminUpdateFreqMessage
        | AdminPollMsg of AdminPollMessage


    let msgToPacket = function
        | AdminJoinMsg { Password = pass; AdminName = name; AdminVersion = version } ->
            createPacketForType PacketType.ADMIN_PACKET_ADMIN_JOIN
            |> writeString pass
            |> writeString name
            |> writeString version
        | AdminUpdateFreqMsg { UpdateType = update; Frequency = freq } ->
            createPacketForType PacketType.ADMIN_PACKET_ADMIN_UPDATE_FREQUENCY
            |> writeU16 (uint16 update)
            |> writeU16 (uint16 freq)
        | AdminPollMsg { UpdateType = update; Data = data } ->
            createPacketForType PacketType.ADMIN_PACKET_ADMIN_POLL
            |> writeByte (byte update)
            |> writeU32 data
           