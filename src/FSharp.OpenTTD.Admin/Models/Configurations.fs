namespace FSharp.OpenTTD.Admin.Models

open System.Net

module Configurations =

    type ServerConfiguration =
        { Host : IPAddress
          Port : int
          Pass : string
          Name : string
          Tag  : string
          Ver  : string }