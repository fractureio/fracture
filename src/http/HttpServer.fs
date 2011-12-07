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

type HttpServer(onRequest) as this = 
    let disposed = ref false

    let rec processData (data, endPoint)= 
        let parser = HttpParser(ParserDelegate(ignore, ignore, requestEnded = fun request -> onRequest (request, response, endPoint)  ))
        parser.Execute( new ArraySegment<_>(data) ) |> ignore
        Seq.empty
    and svr = TcpServer.Create(new Pipelet<_,_>("Parser", processData, Pipelets.basicRouter, 100000, 1000))
    and response = (svr :> IPipeletInput<_>).Post
      
    //ensures the listening socket is shutdown on disposal.
    let cleanUp disposing = 
        if not !disposed then
            if disposing && svr <> Unchecked.defaultof<TcpServer> then
                (svr :> IDisposable).Dispose()
            disposed := true
        
    member h.Start(port) = svr.Listen(IPAddress.Loopback, port)

    interface IDisposable with
        member h.Dispose() =
            cleanUp true
            GC.SuppressFinalize(this)
