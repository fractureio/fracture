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
    let parserCache = new ConcurrentDictionary<_,_>()
    let rec processData (data, endPoint)= 

        let createParser() = HttpParser(ParserDelegate(ignore, ignore, requestEnded = fun request -> 
            onRequest (request, (svr:TcpServer).Send endPoint )  ))

        let parser = parserCache.AddOrUpdate(endPoint, createParser(), (fun key value-> (value))  )

        parser.Execute( new ArraySegment<_>(data) ) |> ignore
        Seq.empty
    and svr = TcpServer.Create(received = new Pipelet<_,_>("Parser", processData, Routers.basicRouter, 100000, 1000), 
                               disconnected = fun endpoint -> 
                                   let (removed, parser) = parserCache.TryRemove(endpoint)
                                   parser.Execute(new ArraySegment<_>()) |> ignore)
      
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
