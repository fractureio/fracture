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

        /// Created a Socket and binds it to address and port and specified connection backlog.
        let createListener (ip:IPAddress, port, backlog) =
            let listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            listener.Bind(new IPEndPoint(ip, port))
            listener.Listen(backlog)
            listener

        ///Creates a Socket as loopback using specified port and connection backlog.
        let listeningSocket = createListener(IPAddress.Loopback, port, backlog)
        let pool = new BocketPool(poolSize, size)
        let clients = new ConcurrentDictionary<_,_>()
        let mutable disposed = false
        
        let closeConnection (sock:Socket) =
            try sock.Shutdown(SocketShutdown.Both)
            finally sock.Close()

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
        
        let aquiredata (args:SocketAsyncEventArgs)= 
            //process received data
            let data:byte[] = Array.zeroCreate args.BytesTransferred
            Buffer.BlockCopy(args.Buffer, args.Offset, data, 0, data.Length)
            data

        ///This function is called when each clients
        ///connects and also on send and receive
        let rec completed (args:SocketAsyncEventArgs) =
            try
                match args.LastOperation with
                | SocketAsyncOperation.Accept -> processAccept(args)
                | SocketAsyncOperation.Receive -> processReceive(args)
                | SocketAsyncOperation.Send -> processSend(args)
                | _ -> args.LastOperation |> failwith "Unknown operation: %a"            
            finally
                args.UserToken <- null
                pool.CheckIn(args)

        and processAccept (args:SocketAsyncEventArgs) =
            let sock = args.AcceptSocket
            if args.SocketError = SocketError.Success then
                //process newly connected client
                let endPoint = sock.RemoteEndPoint :?> IPEndPoint
                let success = clients.TryAdd(endPoint, sock) (*add client to dictionary*)
                if not success then failwith "client could not be added"

                //trigger connected
                connectedEvent.Trigger(endPoint)
                args.AcceptSocket <- null (*remove the AcceptSocket because we will be reusing args*)

                //start next accept
                do listeningSocket.AcceptAsyncSafe(completed, pool.CheckOut())

                //check if data was given on connection
                if args.BytesTransferred > 0 then
                    let data = aquiredata args
                    //trigger received
                    (data, sock.RemoteEndPoint :?> IPEndPoint) |> receivedEvent.Trigger
                
                //start receive on accepted client
                let saea = pool.CheckOut()
                saea.UserToken <- sock
                sock.ReceiveAsyncSafe(completed, saea)
            else args.SocketError.ToString() |> printfn "socket error on accept: %s"

        and processReceive (args:SocketAsyncEventArgs) =
            let sock = args.UserToken :?> Socket
            if args.SocketError = SocketError.Success && args.BytesTransferred > 0 then
                //process received data, check if data was given on connection.
                let data = aquiredata args
                //trigger received
                (data, sock.RemoteEndPoint :?> IPEndPoint) |> receivedEvent.Trigger
                //get on with the next receive
                let saea = pool.CheckOut()
                saea.UserToken <- sock
                sock.ReceiveAsyncSafe( completed, saea)
            else
                //Something went wrong or the client stopped sending bytes.
                sock.RemoteEndPoint :?> IPEndPoint |> disconnectedEvent.Trigger 
                closeConnection sock

        and processSend (args:SocketAsyncEventArgs) =
            let sock = args.UserToken :?> Socket
            match args.SocketError with
            | SocketError.Success ->
                let sentData = aquiredata args
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
            if success then
                let rec loop offset =
                    if offset < msg.Length then
                        let tosend =
                            let remaining = msg.Length - offset in
                            if remaining > size then size else remaining
                        let saea = pool.CheckOut()
                        saea.UserToken <- client
                        Array.blit msg offset saea.Buffer saea.Offset tosend
                        saea.SetBuffer(saea.Offset, tosend)
                        client.SendAsyncSafe(completed, saea)
                        loop (offset + tosend)
                loop 0                    
            else failwith "could not find client %"
        
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