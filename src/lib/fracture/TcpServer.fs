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
    let connectionPool = new BocketPool("connection pool", backlog, 1024)(*288 bytes is the minimum size for a connection*)
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

    let disconnect (sd:SocketDescriptor) =
        !-- connections
        disconnected |> Option.iter (fun x-> x (sd.RemoteEndPoint ))
        sd.Socket.Close()

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

            //trigger connected
            connected |> Option.iter (fun x-> x(endPoint))
            !++ connections
            args.AcceptSocket <- null (*remove the AcceptSocket because we're reusing args*)

            //start next accept, *note connectionPool.CheckOut could block
            let connectionSaea = connectionPool.CheckOut()
            do listeningSocket.AcceptAsyncSafe(completed, connectionSaea)

            //start receive on accepted client
            let receiveSaea = pool.CheckOut()
            receiveSaea.UserToken <- {Socket = acceptSocket; RemoteEndPoint = acceptSocket.RemoteEndPoint :?> IPEndPoint;}
            acceptSocket.ReceiveAsyncSafe(completed, receiveSaea)

            //check if data was given on connection
            if args.BytesTransferred > 0 then
                let data = acquireData args
                //trigger received
                received |> Option.iter (fun x -> x (data, endPoint, send {Socket = acceptSocket; RemoteEndPoint = endPoint} completed pool.CheckOut) )

        else Console.WriteLine (sprintf "socket error on accept: %A" args.SocketError)

    and processDisconnect (args:SocketAsyncEventArgs) =
        let sd = args.UserToken :?> SocketDescriptor
        sd |> disconnect

    and processReceive (args:SocketAsyncEventArgs) =
        let sd = args.UserToken :?> SocketDescriptor
        let socket = sd.Socket
        if args.SocketError = SocketError.Success && args.BytesTransferred > 0 then
            //process received data, check if data was given on connection.
            let data = acquireData args
            //trigger received
            received |> Option.iter (fun x-> x (data, sd.RemoteEndPoint, send sd completed pool.CheckOut))
            //get on with the next receive
            if socket.Connected then 
                let saea = pool.CheckOut()
                saea.UserToken <- sd
                socket.ReceiveAsyncSafe( completed, saea)
            else ()//? what do we do here?
        else
            //Something went wrong or the client stopped sending bytes.
            disconnect(sd)

    and processSend (args:SocketAsyncEventArgs) =
        let sd = args.UserToken :?> SocketDescriptor
        match args.SocketError with
        | SocketError.Success ->
            let sentData = acquireData args
            //notify data sent
            sent |> Option.iter (fun x-> x (sentData, sd.RemoteEndPoint))
        | SocketError.NoBufferSpaceAvailable
        | SocketError.IOPending
        | SocketError.WouldBlock ->
            failwith "Buffer overflow or send buffer timeout" //graceful termination?  
        | _ -> args.SocketError.ToString() |> printfn "socket error on send: %s"

    static member Create(?received, ?connected, ?disconnected, ?sent) =
        new TcpServer(5000, 1024, 1000, ?received = received, ?connected = connected, ?disconnected = disconnected, ?sent = sent)

    ///Sends the specified message to the client.
    member s.Send(clientEndPoint, msg:byte[]) =
        let success, client = clients.TryGetValue(clientEndPoint)
        if success then 
            send {Socket = client;RemoteEndPoint = clientEndPoint}  completed  pool.CheckOut  msg  size
        else failwith "could not find client %"
        
    ///Starts the accepting a incoming connections.
    member s.Listen(?address, ?port) =
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
