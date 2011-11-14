module Fracture.Common

open System
open System.Diagnostics
open System.Net
open System.Net.Sockets
open SocketExtensions

type SocketDescriptor = {Socket: Socket; RemoteEndPoint: IPEndPoint}

/// Creates a Socket and binds it to specified IPEndpoint, if you want a sytem assigned one Use IPEndPoint(IPAddress.Any, 0)
let createSocket (ipEndPoint) =
    let socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    socket.Bind(ipEndPoint);socket

let closeConnection (socket:Socket) =
    try socket.Shutdown(SocketShutdown.Both)
    finally socket.Close()

let disposeSocket (socket:Socket) =
    try
        socket.Shutdown(SocketShutdown.Both)
        socket.Disconnect(false)
        socket.Close()
    with
        // note: the calls above can sometimes result in SocketException.
        :? System.Net.Sockets.SocketException -> ()
    socket.Dispose()

/// Sends data to the socket cached in the SAEA given, using the SAEA's buffer
let send client completed (getArgs: unit -> SocketAsyncEventArgs) bufferLength (msg: byte[]) keepAlive = 
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
    
let acquireData(args: SocketAsyncEventArgs)= 
    //process received data
    let data:byte[] = Array.zeroCreate args.BytesTransferred
    Buffer.BlockCopy(args.Buffer, args.Offset, data, 0, data.Length)
    data
