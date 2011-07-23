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
type TcpClient(ipEndPoint, poolSize, size) =

    ///Creates a Socket as loopback using specified IPEndPoint.
    let listeningSocket = createSocket(ipEndPoint)
    let pool = new BocketPool("regularpool", poolSize, size)
    let mutable disposed = false
        
    //ensures the listening socket is shutdown on disposal.
    let cleanUp() = 
        if not disposed then
            disposed <- true
            disposeSocket listeningSocket
            (pool :> IDisposable).Dispose()

    let connectedEvent = new Event<_>()
    let disconnectedEvent = new Event<_>()
    let sentEvent = new Event<_>()
    let receivedEvent = new Event<_>()
        
    ///This function is called on each async operation.
    let rec completed (args:SocketAsyncEventArgs) =
        try
            match args.LastOperation with
            | SocketAsyncOperation.Connect -> processConnect(args)
            | SocketAsyncOperation.Receive -> processReceive(args)
            | SocketAsyncOperation.Send -> processSend(args)
            //TODO: add disconnect
            | _ -> args.LastOperation |> failwith "Unknown operation: %a"            
        finally
            args.UserToken <- null
            pool.CheckIn(args)

    and processConnect (args:SocketAsyncEventArgs) =
        if args.SocketError = SocketError.Success then

            //trigger connected
            connectedEvent.Trigger(listeningSocket.RemoteEndPoint)
            //args.AcceptSocket <- null (*AcceptSocket is null on a connect, its only set by Accept*)

            //NOTE: On the client this is not received data but the data sent during connect...
            //check if data was given on connection
            //if args.BytesTransferred > 0 then
            //    let data = acquireData args
            //    //trigger received
            //    (data, listeningSocket.RemoteEndPoint :?> IPEndPoint) |> receivedEvent.Trigger
                
            //start receive on accepted client
            let saea = pool.CheckOut()
            args.ConnectSocket.ReceiveAsyncSafe(completed, saea)
        else sprintf "socket error on accept: %A" args.SocketError |> Common.logger

    and processReceive (args:SocketAsyncEventArgs) =
        if args.SocketError = SocketError.Success && args.BytesTransferred > 0 then
            //process received data, check if data was given on connection.
            let data = acquireData args
            //trigger received
            (data, listeningSocket.RemoteEndPoint :?> IPEndPoint) |> receivedEvent.Trigger
            //get on with the next receive
            let saea = pool.CheckOut()
            listeningSocket.ReceiveAsyncSafe (completed,  saea)
        else
            //Something went wrong or the server stopped sending bytes.
            listeningSocket.RemoteEndPoint :?> IPEndPoint |> disconnectedEvent.Trigger 
            closeConnection listeningSocket

    and processSend (args:SocketAsyncEventArgs) =
        let sock = args.UserToken :?> Socket
        match args.SocketError with
        | SocketError.Success ->
            let sentData = acquireData args
            //notify data sent
            (sentData, sock.RemoteEndPoint :?> IPEndPoint) |> sentEvent.Trigger
        | SocketError.NoBufferSpaceAvailable
        | SocketError.IOPending
        | SocketError.WouldBlock ->
            failwith "Buffer overflow or send buffer timeout" //graceful termination?  
        | _ -> sprintf "socket error on send: %A" args.SocketError |> Common.logger

    ///This event is fired when a client connects.
    [<CLIEvent>]member this.Connected = connectedEvent.Publish
    ///This event is fired when a client disconnects.
    [<CLIEvent>]member this.Disconnected = disconnectedEvent.Publish
    ///This event is fired when a message is sent to a client.
    [<CLIEvent>]member this.Sent = sentEvent.Publish
    ///This event is fired when a message is received from a client.
    [<CLIEvent>]member this.Received = receivedEvent.Publish

    ///Sends the specified message to the client.
    member this.Send(msg:ArraySegment<byte>) =
        if listeningSocket.Connected then
           send listeningSocket  completed  pool.CheckOut size  msg
        else listeningSocket.RemoteEndPoint :?> IPEndPoint |> disconnectedEvent.Trigger
        
    ///Starts connecting with remote server
    member this.Start(ipEndPoint) = 
        pool.Start(completed)
        let saea = pool.CheckOut()
        //TODO: look into why a minimum of 1 byte has to be set for
        // a connect to be successful (288 is specified on msdn)
        saea.SetBuffer(saea.Offset, 288)
        saea.RemoteEndPoint <- ipEndPoint
        listeningSocket.ConnectAsyncSafe(completed, saea)

    ///Used to close the current listening socket.
    member this.Close() = cleanUp()
        
    interface IDisposable with
        member this.Dispose() = cleanUp()
        
    ///Creates a new TcpClient that uses a system assigned local endpoint that has 50 receive/sent Bockets and 4096 bytes backing storage for each.
    new() = new TcpClient(new IPEndPoint(IPAddress.Any, 0), 50, 4096)