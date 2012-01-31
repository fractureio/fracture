module Fracture.Common

open System
open System.Diagnostics
open System.Net
open System.Net.Sockets
open SocketExtensions

/// Creates a Socket and binds it to specified IPEndpoint, if you want a sytem assigned one Use IPEndPoint(IPAddress.Any, 0)
let createTcpSocket () =
    let socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    socket

let inline acquireData(args: SocketAsyncEventArgs)= 
    //process received data
    let data:byte[] = Array.zeroCreate args.BytesTransferred
    Buffer.BlockCopy(args.Buffer, args.Offset, data, 0, data.Length)
    data

let discon endPoint (socket:Socket) callback= 
    try
        socket.Close(2)
    with
        // note: the calls above can sometimes result in SocketException.
        | :? System.Net.Sockets.SocketException -> ()  
        | :? System.ObjectDisposedException -> ()
    callback endPoint

/// Sends data to the socket cached in the SAEA given, using the SAEA's buffer
let send (client:Socket) endPoint completed (msg: byte[]) keepAlive (getFromPool: unit -> SocketAsyncEventArgs) disconnectcallback = 
    let rec loop offset =
        let args = getFromPool()
        let amountToSend = min (msg.Length - offset) args.Count
        args.UserToken <- endPoint
        args.AcceptSocket <- client
        Buffer.BlockCopy(msg, offset, args.Buffer, args.Offset, amountToSend)
        args.SetBuffer(args.Offset, amountToSend)

        if not client.Connected then 
            discon endPoint client disconnectcallback

        client.SendAsyncSafe(completed, args)
        let newoffset = offset + amountToSend
        if newoffset < msg.Length then loop (newoffset) 
    loop 0  
    if not keepAlive then
        discon endPoint client disconnectcallback