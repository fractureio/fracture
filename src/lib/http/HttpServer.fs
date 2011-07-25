module Fracture.HttpServer

open System
open System.Net
open Fracture
open Fracture.Common
open HttpMachine
open System.Collections.Generic
open System.Diagnostics

type HttpServer(headers, body) = 
    let svr = TcpServer.Create(fun (data,svr,sd) -> 
        let parserDelegate = ParserDelegate( (fun (a,b) ->headers(a,b,svr,sd)), (fun (data) -> (body(data, svr,sd))), (fun(req)-> ()) )
        let parser = HttpParser(parserDelegate)
        parser.Execute( new ArraySegment<_>(data))|> ignore)
        
    member h.Listen(port) =     
        svr.Listen(port = port)

    member h.Send= svr.Send