namespace Fracture

open System
open System.Diagnostics
open System.Net
open System.Net.Sockets
open System.Collections.Generic
open System.Collections.Concurrent
open SocketExtensions
open Common
open Threading
open Fracture.Pipelets

///Creates a new TcpServer using the specified parameters
type TcpServer(poolSize, perOperationBufferSize, acceptBacklogCount, received, ?connected, ?disconnected, ?sent)=
    let pool = new BocketPool("regular pool", max poolSize 2, perOperationBufferSize)
    let connectionPool = new BocketPool("connection pool", max (acceptBacklogCount * 2) 2, perOperationBufferSize)(*Note: 288 bytes is the minimum size for a connection*)
    let clients = new ConcurrentDictionary<_,_>()
    let connections = ref 0
    let listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    let disposed = ref false
    let receivePipe: IPipeletInput<byte[] * EndPoint> = received
    let errors (ex:Exception) = Console.WriteLine(ex.Message)
       
    /// Ensures the listening socket is shutdown on disposal.
    let cleanUp disposing = 
        if not !disposed then
            if disposing then
                if listeningSocket <> null then
                    disposeSocket listeningSocket
                pool.Dispose()
                connectionPool.Dispose()
            disposed := true

    let disconnect (socket:Socket, endPoint) =
        !-- connections
        if disconnected.IsSome then 
            disconnected.Value endPoint
//        socket.Shutdown(SocketShutdown.Both)
//        if socket.Connected then 
//            try //TODO: make this code more defensive in that the socket we might be connecting to can be disposed of at any time
//                socket.Disconnect(false)
//            finally ()
        let remsocket = ref Unchecked.defaultof<Socket>
        let removed = clients.TryRemove(endPoint, remsocket)
        disposeSocket socket

    ///This function is called on each connect,sends,receive, and disconnect
    let rec completed (args:SocketAsyncEventArgs) =
        try
            if ExecutionContext.IsFlowSuppressed() then ExecutionContext.RestoreFlow()
            try
                match args.LastOperation with
                | SocketAsyncOperation.Accept -> processAccept(args)
                | SocketAsyncOperation.Receive -> processReceive(args)
                | SocketAsyncOperation.Send -> processSend(args)
                | SocketAsyncOperation.Disconnect -> processDisconnect(args)
                | _ -> failwith (sprintf "Unknown operation: %A" args.SocketError)
            with
            | ex -> errors ex
        finally
            args.UserToken <- null
            match args.LastOperation with
            | SocketAsyncOperation.Accept -> connectionPool.CheckIn(args)
            | _ -> pool.CheckIn(args)

    and processAccept (args) =
        match args.SocketError with
        | SocketError.Success ->
            let acceptSocket = args.AcceptSocket
            let endPoint = acceptSocket.RemoteEndPoint

            //start next accept
            let saea = connectionPool.CheckOut()
            //TODO: make this code more defensive in that the socket we might be connecting to can be disposed of at any time
            do listeningSocket.AcceptAsyncSafe(completed, saea)

            //process newly connected client
            clients.AddOrUpdate(endPoint, acceptSocket, fun _ _ -> acceptSocket) |> ignore

            //trigger connected
            connected |> Option.iter (fun x  -> x endPoint)
            !++ connections
            args.AcceptSocket <- null (*remove the AcceptSocket because we're reusing args*)

            //start receive on accepted client
            let receiveSaea = pool.CheckOut()
            receiveSaea.AcceptSocket <- acceptSocket
            receiveSaea.UserToken <- endPoint
            //TODO: make this code more defensive in that the socket we might be connecting to can be disposed of at any time
            acceptSocket.ReceiveAsyncSafe(completed, receiveSaea)

            //check if data was given on connection
            if args.BytesTransferred > 0 then
                let data = acquireData args
                //trigger received
                received.Post(data, endPoint)
        
        | SocketError.OperationAborted
        | SocketError.Disconnecting when !disposed -> ()// stop accepting here, we're being shutdown.
        | _ -> Debug.WriteLine (sprintf "socket error on accept: %A" args.SocketError)
         
    and processDisconnect (args) =
        disconnect(args.AcceptSocket, args.UserToken :?> EndPoint)

    and processReceive (args) =
        let socket = args.AcceptSocket
        let endPoint = args.UserToken :?> EndPoint
        if args.SocketError = SocketError.Success && args.BytesTransferred > 0 then
            //process received data, check if data was given on connection.
            let data = acquireData args
            //trigger received
            received.Post(data, endPoint )
            //get on with the next receive
            if socket.Connected then 
                let saea = pool.CheckOut()
                saea.AcceptSocket <- args.AcceptSocket
                saea.UserToken <- endPoint
                //TODO: make this code more defensive in that the socket we might be connecting to can be disposed of at any time
                socket.ReceiveAsyncSafe( completed, saea)
        //0 byte receive - disconnect.
        else disconnect (socket, endPoint)

    and processSend (args) =
        let endPoint = args.UserToken :?> EndPoint
        match args.SocketError with
        | SocketError.Success ->
            let sentData = acquireData args
            //notify data sent
            sent |> Option.iter (fun x  -> x (sentData, endPoint))
//        Not shure we can even deal with these, drop through to '_'
//        | SocketError.NoBufferSpaceAvailable 
//        | SocketError.IOPending 
//        | SocketError.WouldBlock -> failwith "%s" <| args.SocketError.ToString()
        | _ -> errors <| Exception(String.Concat("Socket Error: ", args.SocketError.ToString()))

    let trySend send client clientEndPoint completed checkout msg keepAlive = 
        async{ send client clientEndPoint completed checkout msg keepAlive}
    
    let sender = new MailboxProcessor<_>(fun inbox ->
        let rec loop() =
            async { let! (clientEndPoint, msg, keepAlive) = inbox.Receive()
                    let success, client = clients.TryGetValue(clientEndPoint)
                    if success && client.Connected then 
                        //wrap exception here with Async.Try? or further down the stack?
                        //another option is to specify an error callback when using the client
                        //this way you could pass in a callback and use it to notify out of this
                        //type.  i.i provive an error callback or event in this type.
                        //TODO: make this code more defensive in that the socket we might be connecting to can be disposed of at any time
                        //Async.StartWithContinuations(trySend send client clientEndPoint completed pool.CheckOut msg keepAlive, ignore, errors, ignore)
                        let result = trySend send client clientEndPoint completed pool.CheckOut msg keepAlive |> Async.Catch |> Async.RunSynchronously
                        match result with
                        | Choice1Of2 _ -> ()
                        | Choice2Of2 ex -> errors ex
                        //send client clientEndPoint completed  pool.CheckOut msg keepAlive
                    else 
                        errors <| Exception(String.Format("could not find client or disconencted. Endpoint:{0}", clientEndPoint.ToString() ) )
                    return! loop() }
        loop() )

    /// PoolSize=10k, Per operation buffer=1k, accept backlog=10000
    static member Create(received, ?connected, ?disconnected, ?sent) =
        new TcpServer(50000, 1024, 10000, received, ?connected = connected, ?disconnected = disconnected, ?sent = sent)

    member s.Connections = connections

    ///Starts the accepting a incoming connections.
    member s.Listen(address: IPAddress, port) =
        //initialise the pool
        pool.Start(completed)
        connectionPool.Start(completed)
        ///starts listening on the specified address and port.
        //This disables nagle: 
        listeningSocket.NoDelay <- true 
        listeningSocket.LingerState <- LingerOption(false, 0)
        listeningSocket.Bind(IPEndPoint(address, port))
        listeningSocket.Listen(acceptBacklogCount)
        for i in 1 .. acceptBacklogCount do
            listeningSocket.AcceptAsyncSafe(completed, connectionPool.CheckOut())
        sender.Start()

    member s.Dispose() = (s :> IDisposable).Dispose()
        
    interface IDisposable with 
        member s.Dispose() =
            cleanUp true
            GC.SuppressFinalize(s)
    
    interface IPipeletInput<EndPoint * byte[] * bool> with
        member s.Post(payload) =
             sender.Post(payload)