namespace Fracture.Http

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Diagnostics
open System.Net
open System.Text
open Fracture
open Fracture.Common
open HttpMachine

type HttpServer(headers, body, requestEnd) as this = 
    let mutable disposed = false

    let svr = TcpServer.Create((fun (data,svr,sd) -> 
        let parser =
            let parserDelegate = ParserDelegate(onHeaders = (fun h -> headers(h,this,sd)), 
                                                requestBody = (fun data -> (body(data, svr,sd))), 
                                                requestEnded = (fun req -> (requestEnd(req, svr, sd))))
            HttpParser(parserDelegate)
        parser.Execute(new ArraySegment<_>(data)) |> ignore))
        
    member h.Start(port) = svr.Listen(IPAddress.Loopback, port)

    member h.Send(client, (response:string), keepAlive) = 
        let encoded = Encoding.ASCII.GetBytes(response)
        svr.Send(client, encoded, keepAlive)

    //ensures the listening socket is shutdown on disposal.
    member private x.Dispose(disposing) = 
        if not disposed then
            if disposing && svr <> Unchecked.defaultof<TcpServer> then
                svr.Dispose()
            disposed <- true

    member h.Dispose() =
        h.Dispose(true)
        GC.SuppressFinalize(this)

    interface IDisposable with
        member h.Dispose() = h.Dispose()
