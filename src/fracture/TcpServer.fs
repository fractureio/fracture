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
namespace Fracture

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Diagnostics
open System.Net
open System.Net.Sockets
open SocketExtensions
open Common
open Threading

///Creates a new TcpServer using the specified parameters
type TcpServer(poolSize, perOperationBufferSize, acceptBacklogCount, received, ?connected, ?disconnected, ?sent) as s=
    let pool = new BocketPool("regular pool", max poolSize 2, perOperationBufferSize)
    let connectionPool = new BocketPool("connection pool", max (acceptBacklogCount * 2) 2, perOperationBufferSize)(*Note: 288 bytes is the minimum size for a connection*)
    let clients = new ConcurrentDictionary<_,_>()
    let connections = ref 0
    let listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    let mutable disposed = false
       
    /// Ensures the listening socket is shutdown on disposal.
    let cleanUp disposing = 
        if not disposed then
            if disposing then
                if listeningSocket <> null then
                    disposeSocket listeningSocket
                pool.Dispose()
                connectionPool.Dispose()
            disposed <- true

    let disconnect (sd:SocketDescriptor) =
        !-- connections
        if disconnected.IsSome then 
            disconnected.Value sd.RemoteEndPoint
        sd.Socket.Shutdown(SocketShutdown.Both)
        if sd.Socket.Connected then sd.Socket.Disconnect(true)

    let processDisconnect (args: SocketAsyncEventArgs) =
        args.UserToken :?> SocketDescriptor |> disconnect

    let processSend (args: SocketAsyncEventArgs) =
        let sd = args.UserToken :?> SocketDescriptor
        match args.SocketError with
        | SocketError.Success ->
            let sentData = acquireData args
            //notify data sent
            sent |> Option.iter (fun x  -> x (sentData, sd.RemoteEndPoint))
        | SocketError.NoBufferSpaceAvailable
        | SocketError.IOPending
        | SocketError.WouldBlock ->
            failwith "Buffer overflow or send buffer timeout" //graceful termination?  
        | _ -> args.SocketError.ToString() |> printfn "socket error on send: %s"

    ///This function is called on each connect,sends,receive, and disconnect
    let rec completed (args:SocketAsyncEventArgs) =
        try
            match args.LastOperation with
            | SocketAsyncOperation.Receive -> processReceive(args)
            | SocketAsyncOperation.Send -> processSend(args)
            | SocketAsyncOperation.Disconnect -> processDisconnect(args)
            | _ -> args.LastOperation |> failwith "Unknown operation: %a"            
        finally
            args.UserToken <- null
            pool.CheckIn(args)
         
    and processReceive (args) =
        let sd = args.UserToken :?> SocketDescriptor
        let socket = sd.Socket
        if args.SocketError = SocketError.Success && args.BytesTransferred > 0 then
            //process received data, check if data was given on connection.
            let data = acquireData args
            //trigger received
            received (data, s, sd )
            //get on with the next receive
            if socket.Connected then 
                let saea = pool.CheckOut()
                saea.UserToken <- sd
                socket.ReceiveAsyncSafe(completed, saea)
        //0 byte receive - disconnect.
        else disconnect sd

    ///This function is called on each connect,sends,receive, and disconnect
    let rec acceptCompleted (args:SocketAsyncEventArgs) =
        try
            match args.LastOperation with
            | SocketAsyncOperation.Accept -> processAccept(args)
            | SocketAsyncOperation.Disconnect -> processDisconnect(args)
            | _ -> args.LastOperation |> failwith "Unknown operation: %a"            
        finally
            args.UserToken <- null
            connectionPool.CheckIn(args)

    and processAccept (args) =
        let acceptSocket = args.AcceptSocket
        match args.SocketError with
        | SocketError.Success ->
              //start next accept
            let saea = connectionPool.CheckOut()
            do listeningSocket.AcceptAsyncSafe(acceptCompleted, saea)

            //process newly connected client
            let endPoint = acceptSocket.RemoteEndPoint :?> IPEndPoint
            clients.AddOrUpdate(endPoint, acceptSocket, fun a b -> (acceptSocket)) |> ignore
            //if not success then failwith "client could not be added"

            //trigger connected
            connected |> Option.iter (fun x  -> x endPoint)
            !++ connections
            args.AcceptSocket <- null (*remove the AcceptSocket because we're reusing args*)

            let sd = {Socket = acceptSocket; RemoteEndPoint = endPoint}

            //start receive on accepted client
            let receiveSaea = pool.CheckOut()
            receiveSaea.UserToken <- sd
            acceptSocket.ReceiveAsyncSafe(completed, receiveSaea)

            //check if data was given on connection
            if args.BytesTransferred > 0 then
                let data = acquireData args
                //trigger received
                received (data, s, sd)
        
        | SocketError.OperationAborted
        | SocketError.Disconnecting when disposed -> ()// stop accepting here, we're being shutdown.
        | _ -> Debug.WriteLine (sprintf "socket error on accept: %A" args.SocketError)
    
    /// PoolSize=10k, Per operation buffer=1k, accept backlog=10000
    static member Create(received, ?connected, ?disconnected, ?sent) =
        new TcpServer(30000, 1024, 10000, received, ?connected = connected, ?disconnected = disconnected, ?sent = sent)

    member s.Connections = connections

    ///Starts the accepting a incoming connections.
    member s.Listen(address: IPAddress, port) =
        //initialise the pool
        pool.Start(completed)
        connectionPool.Start(acceptCompleted)
        ///starts listening on the specified address and port.
        //This disables nagle
        //listeningSocket.NoDelay <- true 
        listeningSocket.Bind(IPEndPoint(address, port))
        listeningSocket.Listen(acceptBacklogCount)
        for i in 1 .. acceptBacklogCount do
            listeningSocket.AcceptAsyncSafe(completed, connectionPool.CheckOut())

    ///Sends the specified message to the client.
    member s.Send(clientEndPoint, msg, keepAlive) =
        let success, client = clients.TryGetValue(clientEndPoint)
        if success then 
            send {Socket = client;RemoteEndPoint = clientEndPoint} completed pool.CheckOut perOperationBufferSize msg keepAlive
        else failwith "could not find client %"
        
    member s.Dispose() =
        cleanUp true
        GC.SuppressFinalize(s)
        
    interface IDisposable with 
        member s.Dispose() = s.Dispose()
