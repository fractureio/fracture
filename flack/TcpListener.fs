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
            
    type TcpListener(maxaccepts, maxsends, maxreceives, size, port, backlog) as this =
        
        let createTcpSocket() =
            new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)

        let createListener (ip:IPAddress, port, backlog) =
            let s = createTcpSocket()
            s.Bind(new IPEndPoint(ip, port))
            s.Listen(backlog)
            s

        let listeningSocket = createListener( IPAddress.Loopback, port, backlog)

        let initPool (maxinpool, callback) =
            let pool = new BlockingCollection<SocketAsyncEventArgs>(maxinpool:int)
            let rec loop n =
                match n with
                | x when x < maxinpool -> 
                    let saea = new SocketAsyncEventArgs()
                    saea.Completed |> Observable.add callback 
                    pool.Add saea
                    loop (n+1)
                | _ -> ()
            loop 0
            pool

        let clients = new ConcurrentDictionary<IPEndPoint, Connection>(4, 1000)

        let mutable disposed = false
        
        let cleanUp() = 
            if not disposed then
                disposed <- true
                listeningSocket.Shutdown(SocketShutdown.Both)
                listeningSocket.Disconnect(false)
                listeningSocket.Close()

        let connectedEvent = new Event<_>()
        let disconnectedEvent = new Event<_>()
        let sentEvent = new Event<_>()
        let receivedEvent = new Event<_>()
        let acceptPool = initPool (maxaccepts, this.acceptcompleted)
        let startAccept = listeningSocket.AcceptAsyncSafe( this.acceptcompleted, acceptPool.Take())

        member private this.acceptcompleted (args : SocketAsyncEventArgs) =
            try
                match args.LastOperation with
                | SocketAsyncOperation.Accept ->
                    match args.SocketError with
                    | SocketError.Success -> 
                        do startAccept
                        let connection = new Connection (maxreceives, maxsends, size, args.AcceptSocket, disconnectedEvent.Trigger, sentEvent.Trigger, receivedEvent.Trigger)
                        let endPoint = args.AcceptSocket.RemoteEndPoint :?> IPEndPoint (*grab remote endpoint*)
                        //trigger connected
                        connectedEvent.Trigger(endPoint)
                        let success = clients.TryAdd(endPoint, connection) (*add client to dictionary*)
                        if not success then 
                            failwith "client could not be added"
                        else
                        connection.Start() (*start the new connection*)
                        args.AcceptSocket <- null (*remove the AcceptSocket because we will be reusing args*)
                    | _ -> 
                        args.SocketError.ToString() |> printfn "socket error on accept: %s"  
                | _ -> args.LastOperation |> failwith "Unknown operation, should be accept but was %a"            
            finally
                acceptPool.Add(args)

        [<CLIEvent>]member this.Connected = connectedEvent.Publish
        [<CLIEvent>]member this.Disconnected = disconnectedEvent.Publish
        [<CLIEvent>]member this.Sent = sentEvent.Publish
        [<CLIEvent>]member this.Received = receivedEvent.Publish

        member this.Send(client, msg:byte[]) =
            let success, client = clients.TryGetValue(client)
            match success with
            | true -> client.Send(msg)
            | _ ->  failwith "could not find client %"

        member this.Start () = startAccept

        member this.Close() =
            cleanUp()

        interface IDisposable with
            member this.Dispose() = cleanUp()

        new(port) = new TcpListener(10,10,10, 1024, port, 100)