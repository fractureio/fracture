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
    let connectionPool = new BocketPool("connection pool", max (acceptBacklogCount * 2) 2, max perOperationBufferSize 288)(*Note: 288 bytes is the minimum size for a connection*)
    let clients = new ConcurrentDictionary<_,_>()
    let connections = ref 0
    let listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    let mutable disposed = false
    let receivePipe: IPipeletInput<byte[] * EndPoint> = received
    let errors (msg:string) = Console.WriteLine(msg)

    /// Ensures the listening socket is shutdown on disposal.
    let cleanUp disposing = 
        if not disposed then
            if disposing then
                if listeningSocket <> null then
                    listeningSocket.Close(2)
                pool.Dispose()
                connectionPool.Dispose()
            disposed <- true
    
    let disconnect ep sock =
        !-- connections
        discon ep sock (fun ep sock ->         
            let remsocket = ref Unchecked.defaultof<Socket>
            clients.TryRemove(ep, remsocket) |> ignore
            disconnected |> Option.iter (fun callback -> callback(ep) ))

    ///This function is called on each connect,sends,receive, and disconnect
    let rec completed (args:SocketAsyncEventArgs) =
        try
            if ExecutionContext.IsFlowSuppressed() then ExecutionContext.RestoreFlow()
            try
                match args.LastOperation with
                | SocketAsyncOperation.Accept -> processAccept(args)
                | SocketAsyncOperation.Receive -> processReceive(args)
                | SocketAsyncOperation.Send -> processSend(args)
                | SocketAsyncOperation.Disconnect -> 
                    processDisconnect(args)
                | _ -> failwith (sprintf "Unknown operation: %A" args.SocketError)
            with
            | ex -> errors ex.Message
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
            acceptSocket.ReceiveAsyncSafe(completed, receiveSaea)

            //check if data was given on connection
            if args.BytesTransferred > 0 then
                let data = acquireData args
                //trigger received
                received.Post(data, endPoint)
        
        | SocketError.OperationAborted
        | SocketError.Disconnecting when disposed -> ()// stop accepting here, we're being shutdown.
        | _ -> errors (sprintf "socket error on accept: %A" args.SocketError)
         
    and processDisconnect (args) =
        let ep = args.UserToken :?> EndPoint
        let sock =  args.AcceptSocket
        disconnect ep sock disconnected

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
                socket.ReceiveAsyncSafe( completed, saea)
        //0 byte receive - disconnect.
        else 
            disconnect endPoint socket disconnected

    and processSend (args) =
        let endPoint = args.UserToken :?> EndPoint
        match args.SocketError with
        | SocketError.Success ->
            //notify data sent
            sent |> Option.iter (fun x  -> x (acquireData args, endPoint))
            //Not shure we can even deal with specific failures, drop through to '_'
        | _ -> errors <| String.Concat("Socket Error: ", args.SocketError.ToString() )

    let trySend send client clientEndPoint completed msg keepAlive getSaea disconnected = 
        async{ send client clientEndPoint completed msg keepAlive getSaea disconnected}

    let trySend2 send client clientEndPoint completed msg keepAlive getSaea disconnected = 
        try
            send client clientEndPoint completed msg keepAlive getSaea disconnected
            Choice1Of2 ()
        with
        | ex -> Choice2Of2 ex
    
    let sender = new MailboxProcessor<_>(fun inbox ->
        let rec loop() =
            async { let! (endPoint, msg, keepAlive) = inbox.Receive()
                    let foundclient, client = clients.TryGetValue(endPoint)
                    if foundclient then 
                        if client.Connected then
                            let result = trySend2 send client endPoint completed msg keepAlive pool.CheckOut (defaultArg disconnected ignore)
//                          let result = trySend send client endPoint completed msg keepAlive pool.CheckOut (defaultArg disconnected ignore) |> Async.Catch |> Async.RunSynchronously
                            match result with
                            | Choice1Of2 _ -> ()
                            | Choice2Of2 ex -> errors ex.Message
                        else errors <| String.Format("Not sending, client:{0} not connected", endPoint.ToString() )
                    else 
                        errors <| String.Format("could not find client:{0}", endPoint.ToString() )
                    return! loop() }
        loop() )

    /// PoolSize=50k, Per operation buffer=1k, accept backlog=1000
    static member Create(received, ?connected, ?disconnected, ?sent) =
        new TcpServer(50000, 1024, 10000, received, ?connected = connected, ?disconnected = disconnected, ?sent = sent)

    member s.Send endPoint data keepAlive = sender.Post(endPoint,data,keepAlive)
    member s.Connections = connections

    ///Starts the accepting a incoming connections.
    member s.Listen(address: IPAddress, port) =
        //initialise the pool
        pool.Start(completed)
        connectionPool.Start(completed)
         
        listeningSocket.ReceiveBufferSize <- 16384
        listeningSocket.SendBufferSize <- 16384
        listeningSocket.NoDelay <- false //This disables nagle on true
        listeningSocket.LingerState <- LingerOption(true, 2)
        listeningSocket.Bind(IPEndPoint(address, port))
        listeningSocket.Listen(acceptBacklogCount)///starts listening on the specified address and port.
        for i in 1 .. acceptBacklogCount do
            listeningSocket.AcceptAsyncSafe(completed, connectionPool.CheckOut())
        sender.Start()

    member s.Dispose() = (s :> IDisposable).Dispose()
        
    interface IDisposable with 
        member s.Dispose() =
            cleanUp true
            GC.SuppressFinalize(s)
    
    interface IPipeletInput<EndPoint * byte[] * bool> with
        member s.Post(payload) = sender.Post(payload)