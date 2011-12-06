module Fracture.Common

open System
open System.Diagnostics
open System.Net
open System.Net.Sockets
open SocketExtensions

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

let dodisconnect( getArgs: unit -> SocketAsyncEventArgs, client:Socket, endPoint:EndPoint, completed) = 
    let args = getArgs()
    args.UserToken <- endPoint
    args.AcceptSocket <- client
    client.Shutdown(SocketShutdown.Both)
    //TODO: make this code more defensive in that the socket we might be connecting to can be disposed of at any time
    client.DisconnectAsyncSafe(completed, args)

/// Sends data to the socket cached in the SAEA given, using the SAEA's buffer
let send (client:Socket) endPoint completed (getArgs: unit -> SocketAsyncEventArgs) (msg: byte[]) keepAlive = 
    let rec loop offset =
        if offset < msg.Length then
            let args = getArgs()
            let amountToSend = min (msg.Length - offset) args.Count
            args.UserToken <- endPoint
            args.AcceptSocket <- client
            Buffer.BlockCopy(msg, offset, args.Buffer, args.Offset, amountToSend)
            args.SetBuffer(args.Offset, amountToSend)
            if client.Connected then 
                client.SendAsyncSafe(completed, args)
                loop (offset + amountToSend)
            else 
                //Console.WriteLine(sprintf "Connection lost to%A" endPoint)
                dodisconnect(getArgs, client, endPoint, completed)       
    loop 0  
    if not keepAlive then 
        dodisconnect(getArgs, client, endPoint, completed)
    
let inline acquireData(args: SocketAsyncEventArgs)= 
    //process received data
    let data:byte[] = Array.zeroCreate args.BytesTransferred
    Buffer.BlockCopy(args.Buffer, args.Offset, data, 0, data.Length)
    data
