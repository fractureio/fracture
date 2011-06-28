namespace Fracture

open System
open System.Net
open System.Net.Sockets
open System.Collections.Generic
open System.Collections.Concurrent
open SocketExtensions
open System.Reflection
open Common

///Creates a new TcpListener using the specified parameters
type TcpListener(ipendpoint, poolSize, size, backlog) =

    ///Creates a Socket as loopback using specified ipendpoint.
    let listeningSocket = createSocket(ipendpoint)
    let pool = new BocketPool(poolSize, size)
    let clients = new ConcurrentDictionary<_,_>()
    let mutable disposed = false
        
    //ensures the listening socket is shutdown on disposal.
    let cleanUp(socket) = 
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
        try
            let sock = args.AcceptSocket
            if args.SocketError = SocketError.Success then
                //process newly connected client
                let endPoint = sock.RemoteEndPoint :?> IPEndPoint
                let success = clients.TryAdd(endPoint, sock) (*add client to dictionary*)
                if not success then failwith "client could not be added"

                //trigger connected
                connectedEvent.Trigger(endPoint)
                args.AcceptSocket <- null (*remove the AcceptSocket because we will be reusing args*)

                //start next accept, *note pool.CheckOut could block
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
            else 
                let data = args.SocketError.ToString()
                Console.WriteLine (sprintf "socket error on accept: %s" data)
        with
        |   e ->
            printfn "%s" e.Message
            Console.ReadKey() |> ignore

    and processReceive (args:SocketAsyncEventArgs) =
        try
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
        with
        |   e ->
            printfn "%s" e.Message
            Console.ReadKey() |> ignore

    and processSend (args:SocketAsyncEventArgs) =
        try
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

        with
        |   e ->
            printfn "%s" e.Message
            Console.ReadKey() |> ignore

    ///This event is fired when a client connects.
    [<CLIEvent>]member this.Connected = connectedEvent.Publish
    ///This event is fired when a client disconnects.
    [<CLIEvent>]member this.Disconnected = disconnectedEvent.Publish
    ///This event is fired when a message is sent to a client.
    [<CLIEvent>]member this.Sent = sentEvent.Publish
    ///This event is fired when a message is received from a client.
    [<CLIEvent>]member this.Received = receivedEvent.Publish

    ///Sends the specified message to the client.
    member this.Send(client, msg:byte[]) =
        let success, client = clients.TryGetValue(client)
        if success then 
            send(client, pool.CheckOut, completed, size, msg)
        else failwith "could not find client %"
        
    ///Starts the accepting a incoming connections.
    member this.Start() = 
        listeningSocket.Listen(backlog)
        pool.Start(completed)
        listeningSocket.AcceptAsyncSafe(completed, pool.CheckOut())

    ///USed to close the current listening socket.
    member this.Close() =
        cleanUp()
        
    interface IDisposable with
        member this.Dispose() = cleanUp()
        
    ///Creates a new TcpListener that listens on the specified port using a backlog of 100, 
    ///50 accept/receive/sent Bockets and 4096 bytes backing storage for each.
    new(port) = new TcpListener(new IPEndPoint(IPAddress.Any, port), 50, 4096, 100)