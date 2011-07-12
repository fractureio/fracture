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
#if callbacks
type TcpServer( poolSize, size, backlog, ?received, ?connected, ?disconnected, ?sent) =
#else
type TcpServer(ipEndPoint, poolSize, size, backlog) =
#endif

    let pool = new BocketPool("regular pool", poolSize, size)
    let connectionPool = new BocketPool("connection pool", backlog, 288)(*288 bytes is the minimum size for a connection*)
    let clients = new ConcurrentDictionary<_,_>()
    let mutable disposed = false
    let connections = ref 0
    let mutable listeningSocket:Socket = null
        
    //ensures the listening socket is shutdown on disposal.
    let cleanUp(socket:Socket) = 
        if not disposed then
            disposed <- true
            socket.Shutdown(SocketShutdown.Both)
            socket.Disconnect(false)
            socket.Close()
            (pool :> IDisposable).Dispose()
            (connectionPool :> IDisposable).Dispose()
    #if callbacks
    #else
    let connectedEvent = new Event<_>()
    let disconnectedEvent = new Event<_>()
    let sentEvent = new Event<_>()
    let receivedEvent = new Event<_>()
    #endif

    let disconnect (socket:Socket) =
        !-- connections
        #if callbacks
        disconnected |> Option.iter (fun x-> x (socket.RemoteEndPoint :?> IPEndPoint))
        #else
        socket.RemoteEndPoint :?> IPEndPoint |> disconnectedEvent.Trigger 
        #endif
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
        let acceptSocket = args.AcceptSocket
        if args.SocketError = SocketError.Success then
            //process newly connected client
            let endPoint = acceptSocket.RemoteEndPoint :?> IPEndPoint
            let success = clients.TryAdd(endPoint, acceptSocket) (*add client to dictionary*)
            if not success then failwith "client could not be added"

            //check if data was given on connection
            if args.BytesTransferred > 0 then
                let data = acquireData args
                //trigger received
                #if callbacks
                received |> Option.iter (fun x -> x (data, acceptSocket.RemoteEndPoint :?> IPEndPoint) )
                #else
                (data, sock.RemoteEndPoint :?> IPEndPoint) |> receivedEvent.Trigger
                #endif

            //trigger connected
            #if callbacks
            connected |> Option.iter (fun x-> x(endPoint))
            #else
            connectedEvent.Trigger(endPoint)
            #endif
            !++ connections
            args.AcceptSocket <- null (*remove the AcceptSocket because we're reusing args*)

            //start next accept, *note connectionPool.CheckOut could block
            let connectionSaea = connectionPool.CheckOut()
            do listeningSocket.AcceptAsyncSafe(completed, connectionSaea)
                
            //start receive on accepted client
            let receiveSaea = pool.CheckOut()
            receiveSaea.UserToken <- acceptSocket
            acceptSocket.ReceiveAsyncSafe(completed, receiveSaea)
        else Console.WriteLine (sprintf "socket error on accept: %A" args.SocketError)

    and processDisconnect (args:SocketAsyncEventArgs) =
        args.UserToken :?> Socket |> disconnect

    and processReceive (args:SocketAsyncEventArgs) =
        let sock = args.UserToken :?> Socket
        if args.SocketError = SocketError.Success && args.BytesTransferred > 0 then
            //process received data, check if data was given on connection.
            let data = acquireData args
            //trigger received
            #if callbacks
            received |> Option.iter (fun x-> x (data, sock.RemoteEndPoint :?> IPEndPoint))
            #else
            (data, sock.RemoteEndPoint :?> IPEndPoint) |> receivedEvent.Trigger
            #endif
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
            #if callbacks
            sent |> Option.iter (fun x-> x (sentData, sock.RemoteEndPoint :?> IPEndPoint))
            #else
            (sentData, sock.RemoteEndPoint :?> IPEndPoint) |> sentEvent.Trigger
            #endif
        | SocketError.NoBufferSpaceAvailable
        | SocketError.IOPending
        | SocketError.WouldBlock ->
            failwith "Buffer overflow or send buffer timeout" //graceful termination?  
        | _ -> args.SocketError.ToString() |> printfn "socket error on send: %s"

    #if callbacks
    #else
    [<CLIEvent>]///This event is fired when a client connects.
    member s.Connected = connectedEvent.Publish

    [<CLIEvent>]///This event is fired when a client disconnects.
    member s.Disconnected = disconnectedEvent.Publish

    [<CLIEvent>]///This event is fired when a message is sent to a client.
    member s.Sent = sentEvent.Publish

    [<CLIEvent>]///This event is fired when a message is received from a client.
    member s.Received = receivedEvent.Publish
    #endif

    ///Sends the specified message to the client.
    member s.Send(client, msg:byte[]) =
        let success, client = clients.TryGetValue(client)
        if success then 
            send(client, pool.CheckOut, completed, size, msg)
        else failwith "could not find client %"
        
    ///Starts the accepting a incoming connections.
    member s.listen(port, ?address) =
        let adr = defaultArg address "127.0.0.1"
        //initialise the pools
        connectionPool.Start(completed)
        pool.Start(completed)
        ///Creates a Socket and starts listening on specifiew address and port.
        listeningSocket <- createSocket(IPEndPoint( IPAddress.Parse(adr), port))
        listeningSocket.Listen(backlog)
        listeningSocket.AcceptAsyncSafe(completed, connectionPool.CheckOut())

    ///Used to close the current listening socket.
    member s.Close(listeningSocket) = 
        cleanUp(listeningSocket)

    member s.Connections = 
        connections
        
    interface IDisposable with 
        member s.Dispose() = cleanUp(listeningSocket)

    //static member createServer( ?receive, ?connected, ?disconnected, ?sent)=
    //    new TcpServer(5000, 4096, 100, receive, connected, disconnected, sent)

    static member createServer( ?received, ?connected, ?disconnected, ?sent)=
        new TcpServer(5000, 4096, 100, ?received = received, ?connected = connected, ?disconnected = disconnected, ?sent = sent)

