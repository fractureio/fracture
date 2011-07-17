module Fracture.Common

open System
open System.Net
open System.Net.Sockets
open SocketExtensions

type SocketDescriptor = {Socket:Socket; RemoteEndPoint:IPEndPoint}

/// Creates a Socket and binds it to specified IPEndpoint, if you want a sytem assigned one Use IPEndPoint(IPAddress.Any, 0)
let createSocket (ipEndPoint) =
    let socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    socket.Bind(ipEndPoint)
    socket

let closeConnection (sock:Socket) =
    try sock.Shutdown(SocketShutdown.Both)
    finally sock.Close()

let send (client:SocketDescriptor) completed (getSaea:unit -> SocketAsyncEventArgs)  (msg:byte[]) maxSize close= 
    let rec loop offset =
        if offset < msg.Length then
            let amountToSend =
                let remaining = msg.Length - offset in
                if remaining > maxSize then maxSize else remaining
            try
                let saea = getSaea()
                saea.UserToken <- client
                Buffer.BlockCopy(msg, offset, saea.Buffer, saea.Offset, amountToSend)
                saea.SetBuffer(saea.Offset, amountToSend)
                client.Socket.SendAsyncSafe(completed, saea)
                loop (offset + amountToSend)
            with
            | e ->
                printfn "%s" e.Message
    loop 0 
    if close then client.Socket.Close(2) 
    
let acquireData(args:SocketAsyncEventArgs)= 
    //process received data
    let data:byte[] = Array.zeroCreate args.BytesTransferred
    Buffer.BlockCopy(args.Buffer, args.Offset, data, 0, data.Length)
    data
