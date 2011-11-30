module Fracture.HttpServer

open System
open System.Text
open System.Net
open Fracture
open Fracture.Common
open HttpMachine
open System.Collections.Generic
open System.Diagnostics
open System.Collections.Concurrent
open Fracture.Pipelets
open Fracture.Http.Core

type HttpServer(headers, body, requestEnd) as this = 
    let disposed = ref false

    let parser = HttpParser(ParserDelegate(onHeaders = headers, 
                                           requestBody = body, 
                                           requestEnded = requestEnd ))

    let recPipe = new Pipelet<_,_>("Parser", (fun a -> parser.Execute( new ArraySegment<_>(fst a) ) |> ignore ; Seq.empty), Pipelets.basicRouter, 10000, 2000)

    let svr = TcpServer.Create(recPipe)

    //ensures the listening socket is shutdown on disposal.
    let cleanUp disposing = 
        if not !disposed then
            if disposing && svr <> Unchecked.defaultof<TcpServer> then
                (svr :> IDisposable).Dispose()
            disposed := true

    let ii = svr :> IPipeletInput<_>
        
    member h.Start(port) = svr.Listen(IPAddress.Loopback, port)

    member h.Send(client, (response:string), keepAlive) = 
        let encoded = Encoding.ASCII.GetBytes(response)
        ii.Post(client, encoded, keepAlive)

    interface IDisposable with
        member h.Dispose() =
            cleanUp true
            GC.SuppressFinalize(this)
