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
    //onHeaders, requestBody, requestEnded
    let x = HttpParser(ParserDelegate(onHeaders =(fun (headers) -> ()), 
                                      requestBody = (fun data -> ()), 
                                      requestEnded = (fun req -> ())))

    //hook parser into x form below
    //parser should notify headers, body, requestEnd, return parser ref so version and keep alive can be found or abstract them inot record type etc
    let recPipe = new Pipelet<_,_>("", (fun a -> x.Execute(new ArraySegment<_>(a)) Seq.singleton a), Pipelets.basicRouter, 10000, 2000)

    let svr = TcpServer.Create(recPipe)
    //TODO feed in the headers, body, requestEnd
//    (fun (data,svr,sd) -> 
//        let parser =
//            let parserDelegate = ParserDelegate(requestBegan =(fun (a,b) -> headers(a,b,this,sd)), 
//                                                requestBody = (fun data -> (body(data, svr,sd))), 
//                                                requestEnded = (fun req -> (requestEnd(req, svr, sd))))
//            HttpParser(parserDelegate)
//        parser.Execute(new ArraySegment<_>(data)) |> ignore))

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
