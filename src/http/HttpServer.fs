//----------------------------------------------------------------------------
//
// Copyright (c) 2011-2012 Dave Thomas (@7sharp9) 
//                         Ryan Riley (@panesofglass)
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//----------------------------------------------------------------------------
namespace Fracture.Http
#nowarn "40"

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Diagnostics
open System.Net
open System.Text
open System.Threading.Tasks
open Fracture
open Fracture.Common
open HttpMachine

[<Sealed>]
type HttpServer(headers, body, requestEnd) as this = 
    let mutable disposed = false
    let parserCache = new ConcurrentDictionary<_,HttpParser>()
    let svr = TcpServer.Create()

    let createParser svr sd =
        let parserDelegate = ParserDelegate(onHeaders = (fun h -> headers(h,this,sd)), 
                                            requestBody = (fun data -> (body(data, svr,sd))), 
                                            requestEnded = (fun req -> (requestEnd(req, svr, sd))))
        HttpParser(parserDelegate)

    let receivedSubscription =
        svr.OnReceived.Subscribe(fun (svr, (data, sd)) -> 
            let parser = parserCache.AddOrUpdate(sd.RemoteEndPoint, createParser svr sd, fun _ value -> value)
            parser.Execute(new ArraySegment<_>(data)) |> ignore)

    let disconnectSubscription =
        svr.OnDisconnected.Subscribe(fun (svr, endPoint) ->
            let removed, parser = parserCache.TryRemove(endPoint)
            if removed then
                parser.Execute(ArraySegment<_>()) |> ignore)

    member h.Start(port) = svr.Listen(IPAddress.Loopback, port)

    member h.Send(client, (response:string), keepAlive) = 
        let encoded = Encoding.ASCII.GetBytes(response)
        svr.Send(client, encoded, keepAlive)

    /// Ensures the listening socket is shutdown on disposal.
    member h.Dispose() =
        receivedSubscription.Dispose()
        disconnectSubscription.Dispose()
        svr.Dispose()
        GC.SuppressFinalize(this)

    interface IDisposable with
        member h.Dispose() = h.Dispose()
