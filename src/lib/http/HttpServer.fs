module Fracture.HttpServer

open System
open System.Net
open Fracture
open Fracture.Common
open HttpMachine
open System.Collections.Generic
open System.Diagnostics
open System.Collections.Concurrent

type HttpServer(headers, body) as this = 

    let svr = TcpServer.Create( (fun (data,svr,sd) -> 

        let createParser  =
            let parserDelegate = ParserDelegate( (fun (a,b) -> headers(a,b,this,sd)), (fun (data) -> (body(data, svr,sd))), (fun(req)-> ()) )
            HttpParser(parserDelegate)

        createParser.Execute( new ArraySegment<_>(data) )|> ignore))
        
    member h.Start(port) =     
        svr.Start(port = port)

    member h.Send(client, (response:string), close) = 
        let encoded = System.Text.Encoding.ASCII.GetBytes(response)
        svr.Send(client, encoded, close)