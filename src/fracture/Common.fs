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
let send client (getArgs: unit -> AsyncSocketEventArgs) bufferLength (msg: byte[]) close = 
    let rec loop offset =
        if offset < msg.Length then
            let args = getArgs()
            let amountToSend = min (msg.Length - offset) bufferLength
            args.UserToken <- client
            Buffer.BlockCopy(msg, offset, args.Buffer, args.Offset, amountToSend)
            args.SetBuffer(args.Offset, amountToSend)
            if client.Socket.Connected then 
                client.Socket.SendAsync(args)
                loop (offset + amountToSend)
            else Debug.WriteLine(sprintf "Connection lost to%A" client.RemoteEndPoint)
    loop 0  
    if close then client.Socket.Close(2)
    
let acquireData(args: AsyncSocketEventArgs)= 
    //process received data
    let data:byte[] = Array.zeroCreate args.BytesTransferred
    Buffer.BlockCopy(args.Buffer, args.Offset, data, 0, data.Length)
    data
