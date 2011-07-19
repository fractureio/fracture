namespace Fracture

open System
open System.Net
open System.Net.Sockets
open System.Collections.Generic
open System.Collections.Concurrent
open SocketExtensions
open Common
open Threading

///Creates a new TcpServer using the specified parameters
type TcpServer( poolSize, size, backlog, ?received, ?connected, ?disconnected, ?sent) =

    let pool = new BocketPool("regular pool", poolSize, size)
    let connectionPool = new BocketPool("connection pool", backlog, 288)(*288 bytes is the minimum size for a connection*)
    let clients = new ConcurrentDictionary<_,_>()
    let mutable disposed = false
    let connections = ref 0
    let mutable listeningSocket:Socket = null
        
    //ensures the listening socket is shutdown on disposal.
    let cleanUp(socket:Socket) = 
        if not disposed && socket <> null then
            disposed <- true
            disposeSocket socket
            (pool :> IDisposable).Dispose()
            (connectionPool :> IDisposable).Dispose()

    let disconnect (socket:Socket) =
        !-- connections
        disconnected |> Option.iter (fun x-> x (socket.RemoteEndPoint :?> IPEndPoint))
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
                received |> Option.iter (fun x -> x (data, acceptSocket.RemoteEndPoint :?> IPEndPoint, send acceptSocket completed pool.CheckOut size) )

            //trigger connected
            connected |> Option.iter (fun x-> x(endPoint))
            !++ connections
            args.AcceptSocket <- null (*remove the AcceptSocket because we're reusing args*)

            //start next accept, *note connectionPool.CheckOut could block
            let connectionSaea = connectionPool.CheckOut()
            do listeningSocket.AcceptAsyncSafe(completed, connectionSaea)
                
            //start receive on accepted client
            let receiveSaea = pool.CheckOut()
            receiveSaea.UserToken <- acceptSocket
            acceptSocket.ReceiveAsyncSafe(completed, receiveSaea)
        else sprintf "socket error on accept: %A" args.SocketError |> Common.logger

    and processDisconnect (args:SocketAsyncEventArgs) =
        args.UserToken :?> Socket |> disconnect

    and processReceive (args:SocketAsyncEventArgs) =
        let sock = args.UserToken :?> Socket
        if args.SocketError = SocketError.Success && args.BytesTransferred > 0 then
            //process received data, check if data was given on connection.
            let data = acquireData args
            //trigger received
            received |> Option.iter (fun x-> x (data, sock.RemoteEndPoint :?> IPEndPoint, send sock completed pool.CheckOut size))
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
            sent |> Option.iter (fun x-> x (sentData, sock.RemoteEndPoint :?> IPEndPoint))
        | SocketError.NoBufferSpaceAvailable
        | SocketError.IOPending
        | SocketError.WouldBlock ->
            failwith "Buffer overflow or send buffer timeout" //graceful termination?  
        | _ -> sprintf "socket error on send: %A" args.SocketError |> Common.logger

    static member Create(?received, ?connected, ?disconnected, ?sent) =
        new TcpServer(5000, 4096, 100, ?received = received, ?connected = connected, ?disconnected = disconnected, ?sent = sent)

    ///Sends the specified message to the client.
    member s.Send(client, msg:byte[]) =
        let success, client = clients.TryGetValue(client)
        if success then 
            send client  completed  pool.CheckOut size  msg
        else failwith "could not find client %A" client.RemoteEndPoint
        
    ///Starts the accepting a incoming connections.
    member s.Listen(?address, ?port) =
        if listeningSocket <> null then invalidOp "this server was already started. it is listening on %A" listeningSocket.LocalEndPoint
        let address = defaultArg address "127.0.0.1"
        let port = defaultArg port 80
        //initialise the pools
        connectionPool.Start(completed)
        pool.Start(completed)
        ///Creates a Socket and starts listening on the specified address and port.
        listeningSocket <- createSocket(IPEndPoint(IPAddress.Parse(address), port))
        listeningSocket.Listen(backlog)
        listeningSocket.AcceptAsyncSafe(completed, connectionPool.CheckOut())

    ///Used to close the current listening socket.
    member s.Close(listeningSocket) = cleanUp(listeningSocket)

    member s.Connections = connections
        
    interface IDisposable with 
        member s.Dispose() = cleanUp(listeningSocket)
