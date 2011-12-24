module Fracture.HttpServer

open System
open System.Text
open System.Net
open System.Threading.Tasks
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
        Console.WriteLine(sprintf "Disconnect from %s" <| endPoint.ToString())
        parserCache.TryRemove(endPoint) 
        |> fun (removed, parser: HttpParser) -> 
            if removed then
                parser.Execute(ArraySegment<_>()) |> ignore

    let rec svr = TcpServer.Create(received = onReceive, disconnected = onDisconnect)
    and createParser endPoint = 
        HttpParser(ParserDelegate(onHeaders = (fun headers -> (Console.WriteLine(sprintf "Headers: %A" headers.Headers))), //NOTE: on ab.exe without the keepalive option only the headers callback fires
                                  requestBody = (fun body -> (Console.WriteLine(sprintf "Body: %A" body))),
                                  requestEnded = fun req -> onRequest( req, (svr:TcpServer).Send endPoint req.RequestHeaders.KeepAlive) 
                   ))

    and onReceive: Func<_,_> = 
        Func<_,_>( fun (endPoint, data) -> Task.Factory.StartNew(fun () ->
        parserCache.AddOrUpdate(endPoint, createParser endPoint, fun _ value -> value )
        |> fun parser -> parser.Execute( ArraySegment(data) ) |> ignore))

    
    
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
