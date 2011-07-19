module Fracture.Common

open System
open System.Net.Sockets
open SocketExtensions

/// Enables 3rd parties to inject their own logging framework for receiving errors
let mutable logger : string->unit = fun message -> Console.Error.WriteLine(message)

/// Creates a Socket and binds it to specified IPEndpoint, if you want a sytem assigned one Use IPEndPoint(IPAddress.Any, 0)
let createSocket (ipEndPoint) =
    let socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    socket.Bind(ipEndPoint)
    socket

let closeConnection (sock:Socket) =
    try sock.Shutdown(SocketShutdown.Both)
    finally sock.Close()

let disposeSocket (socket:Socket) =
    try
        socket.Shutdown(SocketShutdown.Both)
        socket.Disconnect(false)
        socket.Close()
    with
        // note: the calls above can sometimes result in
        // SocketException: A request to send or receive data was disallowed because the socket
        // is not connected and (when sending on a datagram socket using a sendto call) no 
        // address was supplied
        :? System.Net.Sockets.SocketException -> ()
    socket.Dispose()

/// Sends data to the socket cached in the SAEA given, using the SAEA's buffer
let send (client:Socket) completed (getSaea:unit -> SocketAsyncEventArgs) bufferLength (msg:byte[]) = 
    let rec loop offset =
        if offset < msg.Length then
            let saea = getSaea()
            let amountToSend = min (msg.Length - offset) bufferLength
            saea.UserToken <- client
            Buffer.BlockCopy(msg, offset, saea.Buffer, saea.Offset, amountToSend)
            saea.SetBuffer(saea.Offset, amountToSend)
            if client.Connected then client.SendAsyncSafe(completed, saea)
                                     loop (offset + amountToSend)
            else sprintf "Connection lost %A->%A" client.LocalEndPoint client.RemoteEndPoint |> logger
    loop 0  
    
let acquireData(args:SocketAsyncEventArgs)= 
    //process received data
    let data:byte[] = Array.zeroCreate args.BytesTransferred
    Buffer.BlockCopy(args.Buffer, args.Offset, data, 0, data.Length)
    data

let remoteEndPointSafe(sock:Socket) =
    try sock.RemoteEndPoint :?> System.Net.IPEndPoint with _ -> null