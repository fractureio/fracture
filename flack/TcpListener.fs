namespace flack
    open System
    open System.Net
    open System.Net.Sockets
    open System.Collections.Generic
    open System.Collections.Concurrent
    open SocketExtensions
    
    open System.Reflection
    [<assembly: AssemblyVersion("0.1.0.*")>] 
    do()
            
    ///Creates a new TcpListener using the specified parameters
    type TcpListener(poolSize, size, port, backlog) =
        
        /// Creates a Tcp Socket using Internetwork Addressing and Stream Type.
        let createTcpSocket() =
            new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)

        /// Created a Socket and binds it to address and port and specified connection backlog.
        let createListener (ip:IPAddress, port, backlog) =
            let s = createTcpSocket()
            s.Bind(new IPEndPoint(ip, port))
            s.Listen(backlog);s

        ///Creates a Socket as loopback using specified port and connection backlog.
        let listeningSocket = createListener(IPAddress.Loopback, port, backlog)
        let pool = new BocketPool(poolSize, size)
        let clients = new ConcurrentDictionary<_,_>()
        let mutable disposed = false
        
        //ensures the listening socket is shutdown on disposal.
        let cleanUp() = 
            if not disposed then
                disposed <- true
                listeningSocket.Shutdown(SocketShutdown.Both)
                listeningSocket.Disconnect(false)
                listeningSocket.Close()
                (pool :> IDisposable).Dispose()

        let connectedEvent = new Event<_>()
        let disconnectedEvent = new Event<_>()
        let sentEvent = new Event<_>()
        let receivedEvent = new Event<_>()
        
        ///This function is called when each connection from clients is accepted and
        ///is responsible receiving and sending to the clients.
        let rec completed (args : SocketAsyncEventArgs) =
            try
                let sock = args.AcceptSocket
                match args.LastOperation with
                | SocketAsyncOperation.Accept -> processAccept (args, sock)
                | SocketAsyncOperation.Receive -> processReceive (args, sock)
                | SocketAsyncOperation.Send -> processSend (args, sock)
                | _ -> args.LastOperation |> failwith "Unknown operation: %a"            
            finally
                args.UserToken <- null
                pool.CheckIn(args)

        and processAccept (args:SocketAsyncEventArgs, sock) =
            match args.SocketError with
            | SocketError.Success -> 
                let endPoint = sock.RemoteEndPoint :?> IPEndPoint
                //trigger connected
                connectedEvent.Trigger(endPoint)
                let success = clients.TryAdd(endPoint, sock) (*add client to dictionary*)
                if not success then failwith "client could not be added"
                args.AcceptSocket <- null (*remove the AcceptSocket because we will be reusing args*)
                let saea = pool.CheckOut()
                saea.UserToken <- sock
                sock.ReceiveAsyncSafe(completed, saea)
                //start next accept, maybe switch round
                do listeningSocket.AcceptAsyncSafe(completed, pool.CheckOut())
            | _ -> args.SocketError.ToString() |> printfn "socket error on accept: %s"

        and processReceive (args:SocketAsyncEventArgs, sock) =
            match args.SocketError with
            | SocketError.Success ->
                //process received data
                let data:byte[] = Array.zeroCreate args.BytesTransferred
                Buffer.BlockCopy(args.Buffer, args.Offset, data, 0, data.Length)
                //notify data received
                (data, sock.RemoteEndPoint :?> IPEndPoint) |> receivedEvent.Trigger
                //get on with the next receive
                let saea = pool.CheckOut()
                saea.UserToken <- sock
                sock.ReceiveAsyncSafe( completed, saea)
            | _ -> sock.RemoteEndPoint :?> IPEndPoint |> disconnectedEvent.Trigger 

        and processSend (args:SocketAsyncEventArgs, sock) =
            match args.SocketError with
            | SocketError.Success ->
                let sentData:byte[] = Array.zeroCreate args.BytesTransferred
                Buffer.BlockCopy(args.Buffer, args.Offset, sentData, 0, sentData.Length);
                //notify data sent
                (sentData, sock.RemoteEndPoint :?> IPEndPoint) |> sentEvent.Trigger
            | SocketError.NoBufferSpaceAvailable
            | SocketError.IOPending
            | SocketError.WouldBlock ->
                failwith "Buffer overflow or send buffer timeout" //graceful termination?  
            | _ -> args.SocketError.ToString() |> printfn "socket error on send: %s"

        [<CLIEvent>]
        ///This event is fired when a client connects.
        member this.Connected = connectedEvent.Publish
        [<CLIEvent>]
        ///This event is fired when a client disconnects.
        member this.Disconnected = disconnectedEvent.Publish
        [<CLIEvent>]
        ///This event is fired when a message is sent to a client.
        member this.Sent = sentEvent.Publish
        [<CLIEvent>]
        ///This event is fired when a message is received from a client.
        member this.Received = receivedEvent.Publish

        ///Sends the specified message to the client.
        member this.Send(client, msg:byte[]) =
            let success, client = clients.TryGetValue(client)
            match success with
            // TODO: write async send here.
            | true -> client.Send(msg)
            | _ ->  failwith "could not find client %"
        
        ///Starts the accepting a incoming connections.
        member this.Start() = 
            pool.Start(completed)
            listeningSocket.AcceptAsyncSafe(completed, pool.CheckOut())

        ///USed to close the current listening socket.
        member this.Close() =
            cleanUp()
        
        interface IDisposable with
            member this.Dispose() = cleanUp()
        
        ///Creates a new TcpListener that listens on the specified port using a backlog of 100, 
        ///50 accept/receive/sent Bockets and 4096 bytes backing storage for each.
        new(port) = new TcpListener(50, 4096, port, 100)