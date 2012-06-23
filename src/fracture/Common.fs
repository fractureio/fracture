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
module Fracture.Common

open System
open System.Diagnostics
open System.Net
open System.Net.Sockets
open SocketExtensions

type SocketDescriptor = {Socket: Socket; RemoteEndPoint: IPEndPoint}

/// Creates a Socket and binds it to specified IPEndpoint, if you want a sytem assigned one Use IPEndPoint(IPAddress.Any, 0)
let inline createSocket (ipEndPoint) =
    let socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    socket.Bind(ipEndPoint);socket

let inline closeConnection (socket:Socket) =
    try socket.Shutdown(SocketShutdown.Both)
    finally socket.Close()

let inline disposeSocket (socket:Socket) =
    try
        socket.Shutdown(SocketShutdown.Both)
        socket.Disconnect(false)
        socket.Close()
    with
        // note: the calls above can sometimes result in SocketException.
        :? System.Net.Sockets.SocketException -> ()
    socket.Dispose()

/// Sends data to the socket cached in the SAEA given, using the SAEA's buffer
let inline send client completed (getArgs: unit -> SocketAsyncEventArgs) bufferLength (msg: byte[]) keepAlive = 
    let rec loop offset =
        if offset < msg.Length then
            let args = getArgs()
            let amountToSend = min (msg.Length - offset) bufferLength
            args.UserToken <- client
            Buffer.BlockCopy(msg, offset, args.Buffer, args.Offset, amountToSend)
            args.SetBuffer(args.Offset, amountToSend)
            if client.Socket.Connected then 
                client.Socket.SendAsyncSafe(completed, args)
                loop (offset + amountToSend)
            else Console.WriteLine(sprintf "Connection lost to%A" client.RemoteEndPoint)
    loop 0  
    if not keepAlive then 
        let args = getArgs()
        args.UserToken <- client
        client.Socket.Shutdown(SocketShutdown.Both)
        client.Socket.DisconnectAsyncSafe(completed, args)
    
let inline acquireData(args: SocketAsyncEventArgs)= 
    //process received data
    let data:byte[] = Array.zeroCreate args.BytesTransferred
    Buffer.BlockCopy(args.Buffer, args.Offset, data, 0, data.Length)
    data
