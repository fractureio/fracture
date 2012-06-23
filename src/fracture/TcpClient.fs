//----------------------------------------------------------------------------
//
// Copyright (c) 2011-2012 Dave Thomas (@7sharp9) Ryan Riley (@panesofglass)
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
namespace Fracture

open System
open System.Net
open System.Net.Sockets
open System.Collections.Generic
open System.Collections.Concurrent
open SocketExtensions
open System.Reflection
open Common

///Creates a new TcpClient using the specified parameters
type TcpClient(poolSize, size) =

    ///Creates a Socket as loopback using specified IPEndPoint.
    let listeningSocket = createTcpSocket()
    let pool = new BocketPool("regularpool", poolSize, size)
    let disposed = ref false
        
    //ensures the listening socket is shutdown on disposal.
    let cleanUp disposing = 
        if not !disposed then
            if disposing then
                listeningSocket.Close(2)
                pool.Dispose()
            disposed := true

    let connectedEvent = new Event<_>()
    let disconnectedEvent = new Event<_>()
    let sentEvent = new Event<_>()
    let receivedEvent = new Event<_>()
    let mutable serverEndPoint = Unchecked.defaultof<EndPoint>
        
    ///This function is called on each async operation.
    let rec completed (args:SocketAsyncEventArgs) =
        try
            match args.LastOperation with
            | SocketAsyncOperation.Connect -> processConnect args
            | SocketAsyncOperation.Receive -> processReceive args
            | SocketAsyncOperation.Send -> processSend args
            //TODO: add disconnect
            | _ -> args.LastOperation |> failwith "Unknown operation: %a"            
        finally
            args.UserToken <- null
            pool.CheckIn(args)

    and processConnect args =
        if args.SocketError = SocketError.Success then
            serverEndPoint <- listeningSocket.RemoteEndPoint
            //trigger connected
            connectedEvent.Trigger(serverEndPoint)
                
            //start receive on accepted client
            let nextArgs = pool.CheckOut()
            args.ConnectSocket.ReceiveAsyncSafe(completed, nextArgs)
        else args.SocketError.ToString() |> printfn "socket error on accept: %s"

    and processReceive args =
        if args.SocketError = SocketError.Success && args.BytesTransferred > 0 then
            //process received data, check if data was given on connection.
            let data = acquireData args
            //trigger received
            (data, serverEndPoint) |> receivedEvent.Trigger
            //get on with the next receive
            let nextArgs = pool.CheckOut()
            listeningSocket.ReceiveAsyncSafe (completed,  nextArgs)
        else
            //Something went wrong or the server stopped sending bytes.
            serverEndPoint |> disconnectedEvent.Trigger 
            discon serverEndPoint listeningSocket disconnectedEvent.Trigger

    and processSend args =
        let sock = args.UserToken :?> Socket
        match args.SocketError with
        | SocketError.Success ->
            let sentData = acquireData args
            //notify data sent
            (sentData, serverEndPoint) |> sentEvent.Trigger
        | SocketError.NoBufferSpaceAvailable
        | SocketError.IOPending
        | SocketError.WouldBlock ->
            failwith "Buffer overflow or send buffer timeout" //graceful termination?  
        | _ -> args.SocketError.ToString() |> printfn "socket error on send: %s"

    ///This event is fired when a client connects.
    [<CLIEvent>]member this.Connected = connectedEvent.Publish
    ///This event is fired when a client disconnects.
    [<CLIEvent>]member this.Disconnected = disconnectedEvent.Publish
    ///This event is fired when a message is sent to a client.
    [<CLIEvent>]member this.Sent = sentEvent.Publish
    ///This event is fired when a message is received from a client.
    [<CLIEvent>]member this.Received = receivedEvent.Publish

    ///Sends the specified message to the client.
    member this.Send(msg:byte[], (keepalive:bool)) =
        if listeningSocket.Connected then
            send listeningSocket serverEndPoint completed msg keepalive pool.CheckOut disconnectedEvent.Trigger
        else discon listeningSocket.RemoteEndPoint listeningSocket disconnectedEvent.Trigger
        
    ///Starts connecting with remote server
    member this.Start(ipEndPoint) = 
        pool.Start(completed)
        let args = pool.CheckOut()
        //TODO: look into why a minimum of 1 byte has to be set for
        // a connect to be successful (288 is specified on msdn)
        args.SetBuffer(args.Offset, 288)
        args.RemoteEndPoint <- ipEndPoint
        listeningSocket.ConnectAsyncSafe(completed, args)

    ///Used to close the current listening socket.
    member this.Dispose() = (this :> IDisposable).Dispose()

    override this.Finalize() = cleanUp false
        
    interface IDisposable with
        member this.Dispose() =
            cleanUp true
            GC.SuppressFinalize(this)
        
    ///Creates a new TcpClient that uses a system assigned local endpoint that has 50 receive/sent Bockets and 4096 bytes backing storage for each.
    new() = new TcpClient(50, 4096)
