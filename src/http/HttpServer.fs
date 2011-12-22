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

    let onDisconnect endPoint = 
        parserCache.TryRemove(endPoint) 
        |> fun (removed, parser: HttpParser) -> 
            if removed then parser.Execute(ArraySegment<_>()) |> ignore

    let rec createParser endPoint = 
        HttpParser(ParserDelegate(requestEnded = fun req -> onRequest( req, (svr:TcpServer).Send endPoint )))
    and onReceive endPoint data = 
        parserCache.AddOrUpdate(endPoint, createParser endPoint, fun key value -> value )
        |> fun e -> e.Execute( ArraySegment(data) ) |> ignore
    and svr = TcpServer.Create(received = onReceive, disconnected = onDisconnect)
    
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
