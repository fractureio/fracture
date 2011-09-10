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
    let mutable disposed = false

    let svr = TcpServer.Create((fun (data,svr,sd) -> 
        let parser =
            let parserDelegate = ParserDelegate((fun (a,b) -> headers(a,b,this,sd)), (fun data -> (body(data, svr,sd))), (fun req -> ()))
            HttpParser(parserDelegate)
        parser.Execute(new ArraySegment<_>(data)) |> ignore))

    //ensures the listening socket is shutdown on disposal.
    let cleanUp disposing = 
        if not disposed then
            if disposing && svr <> Unchecked.defaultof<TcpServer> then
                (svr :> IDisposable).Dispose()
            disposed <- true
        
    member h.Start(port) =     
        svr.Start(port = port)

    member h.Send(client, (response:string), close) = 
        let encoded = System.Text.Encoding.ASCII.GetBytes(response)
        svr.Send(client, encoded, close)

    interface IDisposable with
        member h.Dispose() =
            cleanUp true
            GC.SuppressFinalize(this)
