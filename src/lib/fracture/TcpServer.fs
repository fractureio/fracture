namespace Fracture

open System
open System.Net
open System.Net.Sockets
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading
open SocketExtensions
open System.Reflection
open Common
open Threading

///Creates a new TcpServer using the specified parameters
type TcpServer(ipEndPoint, poolSize, size, backlog) =

    ///Creates a Socket as loopback using specified ipEndPoint.
    let listeningSocket = createSocket(ipEndPoint)
    let pool = new BocketPool("regular pool", poolSize, size)
    let connectionPool = new BocketPool("connection pool", backlog, 288)(*288 bytes is the minimum size for a connection*)
    let clients = new ConcurrentDictionary<_,_>()
    let mutable disposed = false
    let connections = ref 0
        
    //ensures the listening socket is shutdown on disposal.
    let cleanUp(socket:Socket) = 
        if not disposed then
            disposed <- true
            socket.Shutdown(SocketShutdown.Both)
            socket.Disconnect(false)
            socket.Close()
            (pool :> IDisposable).Dispose()
            (connectionPool :> IDisposable).Dispose()

    let connectedEvent = new Event<_>()
    let disconnectedEvent = new Event<_>()
    let sentEvent = new Event<_>()
    let receivedEvent = new Event<_>()

    let disconnect (socket:Socket) =
        !-- connections
        socket.RemoteEndPoint :?> IPEndPoint |> disconnectedEvent.Trigger 
        closeConnection socket

    ///This function is called when each clients connects and also on send and receive
    let rec completed (args:SocketAsyncEventArgs) =
        try
            match args.LastOperation with
            | SocketAsyncOperation.Accept -> processAccept(args)
            | SocketAsyncOperation.Receive -> processReceive(args)
            | SocketAsyncOperation.Send -> processSend(args)
            | SocketAsyncOperation.Disconnect -> processDisconnect(args)
            | _ -> args.LastOperation |> failwith "Unknown operation: %a"            
        finally
            args.UserToken <- null
            let getpool = 
                function 
                | SocketAsyncOperation.Accept -> connectionPool
                | _ -> pool
            args.LastOperation |> getpool |> fun p -> p.CheckIn(args)

    and processAccept (args:SocketAsyncEventArgs) =
        let sock = args.AcceptSocket
        if args.SocketError = SocketError.Success then
            //process newly connected client
            let endPoint = sock.RemoteEndPoint :?> IPEndPoint
            let success = clients.TryAdd(endPoint, sock) (*add client to dictionary*)
            if not success then failwith "client could not be added"

            //check if data was given on connection
            if args.BytesTransferred > 0 then
                let data = acquireData args
                //trigger received
                (data, sock.RemoteEndPoint :?> IPEndPoint) |> receivedEvent.Trigger

            //trigger connected
            connectedEvent.Trigger(endPoint)
            !++ connections
            args.AcceptSocket <- null (*remove the AcceptSocket because we're reusing args*)

            //start next accept, *note pool.CheckOut could block
            do listeningSocket.AcceptAsyncSafe(completed, connectionPool.CheckOut())
                
            //start receive on accepted client
            let saea = pool.CheckOut()
            saea.UserToken <- sock
            sock.ReceiveAsyncSafe(completed, saea)
        else Console.WriteLine (sprintf "socket error on accept: %A" args.SocketError)

    and processDisconnect (args:SocketAsyncEventArgs) =
        args.UserToken :?> Socket |> disconnect

    and processReceive (args:SocketAsyncEventArgs) =
        let sock = args.UserToken :?> Socket
        if args.SocketError = SocketError.Success && args.BytesTransferred > 0 then
            //process received data, check if data was given on connection.
            let data = acquireData args
            //trigger received
            (data, sock.RemoteEndPoint :?> IPEndPoint) |> receivedEvent.Trigger
            //get on with the next receive
            let saea = pool.CheckOut()
            saea.UserToken <- sock
            sock.ReceiveAsyncSafe( completed, saea)
        else
            //Something went wrong or the client stopped sending bytes.
            disconnect(sock)

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
        | _ -> args.SocketError.ToString() |> printfn "socket error on send: %s"

    [<CLIEvent>]///This event is fired when a client connects.
    member s.Connected = connectedEvent.Publish

    [<CLIEvent>]///This event is fired when a client disconnects.
    member s.Disconnected = disconnectedEvent.Publish

    [<CLIEvent>]///This event is fired when a message is sent to a client.
    member s.Sent = sentEvent.Publish

    [<CLIEvent>]///This event is fired when a message is received from a client.
    member s.Received = receivedEvent.Publish

    ///Sends the specified message to the client.
    member s.Send(client, msg:byte[]) =
        let success, client = clients.TryGetValue(client)
        if success then 
            send(client, pool.CheckOut, completed, size, msg)
        else failwith "could not find client %"
        
    ///Starts the accepting a incoming connections.
    member s.Start() =
        connectionPool.Start(completed)
        pool.Start(completed)
        listeningSocket.Listen(backlog)
        listeningSocket.AcceptAsyncSafe(completed, connectionPool.CheckOut())

    ///USed to close the current listening socket.
    member s.Close() = cleanUp(listeningSocket)

    member s.Connections = connections
        
    interface IDisposable with 
        member s.Dispose() = cleanUp(listeningSocket)
        
    ///Creates a new TcpListener that listens on the specified port using a backlog of 100, 
    ///1000 receive/sent Bockets, 4096 bytes backing storage for each.
    new(port) = new TcpServer(new IPEndPoint(IPAddress.Any, port), 1000, 4096, 100)