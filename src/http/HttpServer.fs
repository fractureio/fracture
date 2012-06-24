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
open Fracture.Pipelets
open FSharp.Control
open HttpMachine
open Owin

[<Sealed>]
type HttpServer(app) as this = 
    let parserCache = new ConcurrentDictionary<_,HttpParser>()
    let tcp = new TcpServer()
    let send client keepAlive data = tcp.Send(client, data, keepAlive)
        
    let createParser sd =
        let parserDelegate = ParserDelegate(app, send sd.RemoteEndPoint)
        HttpParser(parserDelegate)

    let receivedSubscription =
        tcp.OnReceived.Subscribe(fun (_, (data, sd)) -> 
            let parser = parserCache.AddOrUpdate(sd.RemoteEndPoint, createParser sd, fun _ value -> value)
            parser.Execute(new ArraySegment<_>(data)) |> ignore)

    let disconnectSubscription =
        tcp.OnDisconnected.Subscribe(fun (_, sd) ->
            let removed, parser = parserCache.TryRemove(sd.RemoteEndPoint)
            if removed then
                parser.Execute(ArraySegment<_>()) |> ignore)
        
    member h.Start(port) = tcp.Listen(IPAddress.Loopback, port)

    /// Ensures the listening socket is shutdown on disposal.
    member h.Dispose() =
        receivedSubscription.Dispose()
        disconnectSubscription.Dispose()
        tcp.Dispose()
        GC.SuppressFinalize(this)

    interface IDisposable with
        member h.Dispose() = h.Dispose()
