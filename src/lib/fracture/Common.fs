module Fracture.Common
open System
open System.Net.Sockets
open SocketExtensions
open System.Reflection

[<assembly: AssemblyVersion("0.1.0.*")>] 
do()

//Common Socket functions

/// Creates a Socket and binds it to specified IPEndpoint, if you want a sytem assigned one Use IPEndPoint(IPAddress.Any, 0)
let createSocket (ipendpoint) =
    let socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    socket.Bind(ipendpoint)
    socket

let closeConnection (sock:Socket) =
    try sock.Shutdown(SocketShutdown.Both)
    finally sock.Close()

let send( client:Socket, getsaea:unit -> SocketAsyncEventArgs, completed, maxSize, msg:byte[])= 
    let rec loop offset =
        if offset < msg.Length then
            let tosend =
                let remaining = msg.Length - offset in
                if remaining > maxSize then maxSize else remaining
            let saea = getsaea()
            saea.UserToken <- client
            Buffer.BlockCopy(msg, offset, saea.Buffer, saea.Offset, tosend)
            saea.SetBuffer(saea.Offset, tosend)
            client.SendAsyncSafe(completed, saea)
            loop (offset + tosend)
    loop 0  
    
let aquiredata (args:SocketAsyncEventArgs)= 
    //process received data
    let data:byte[] = Array.zeroCreate args.BytesTransferred
    Buffer.BlockCopy(args.Buffer, args.Offset, data, 0, data.Length)
    data