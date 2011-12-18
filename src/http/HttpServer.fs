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

type FP =
    static member curry2 (f:_*_ -> _) = 
        fun a b -> f (a,b)
    static member curry3 (f:_*_*_ -> _) = 
        fun a b c -> f (a,b,c)

type HttpServer(onRequest) as this = 
    let disposed = ref false
    let parserCache = new ConcurrentDictionary<_,_>()
    let rec processData (data, endPoint)= 
        let myIndianCurry = FP.curry3 (svr:TcpServer).Send 
        let createParser() = HttpParser(ParserDelegate(ignore, ignore, requestEnded = fun request -> 
            onRequest (request, myIndianCurry endPoint )  ))

        let parser = parserCache.AddOrUpdate(endPoint, createParser(), (fun key value-> (value))  )

        parser.Execute( new ArraySegment<_>(data) ) |> ignore
    and svr = TcpServer.Create(received = processData, 
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
