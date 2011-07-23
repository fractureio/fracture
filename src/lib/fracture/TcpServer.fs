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
type TcpServer(poolSize, perOperationBufferSize, acceptBacklogCount, received, ?connected, ?disconnected, ?sent) as s=
    let connected = defaultArg connected (fun ep -> Console.WriteLine(sprintf "%A %A: Connected" DateTime.UtcNow.TimeOfDay ep))
    let disconnected = defaultArg disconnected (fun ep -> Console.WriteLine(sprintf "%A %A: Disconnected" DateTime.UtcNow.TimeOfDay ep))
    let sent = defaultArg sent (fun (received:byte[], ep) -> Console.WriteLine( sprintf  "%A Sent: %A " DateTime.UtcNow.TimeOfDay received.Length ))

    let pool = new BocketPool("regular pool", poolSize, perOperationBufferSize)
    let clients = new ConcurrentDictionary<_,_>()
    let connections = ref 0
    let listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    let mutable disposed = false
        
    //ensures the listening socket is shutdown on disposal.
    let cleanUp(socket:Socket) = 
        if not disposed && socket <> null then
            disposed <- true
            disposeSocket socket
            (pool :> IDisposable).Dispose()

    let disconnect (sd:SocketDescriptor) =
        !-- connections
        disconnected sd.RemoteEndPoint
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
            pool.CheckIn(args)

    and processAccept (args:SocketAsyncEventArgs) =
        let acceptSocket = args.AcceptSocket
        match args.SocketError with
        | SocketError.Success ->

            //start next accept, *note connectionPool.CheckOut could block
            //Async.Start(async{let saea = pool.CheckOut()
            //                  do listeningSocket.AcceptAsyncSafe(completed, saea)})
            let saea = pool.CheckOut()
            do listeningSocket.AcceptAsyncSafe(completed, saea)

            //process newly connected client
            let endPoint = acceptSocket.RemoteEndPoint :?> IPEndPoint
            let success = clients.TryAdd(endPoint, acceptSocket) (*add client to dictionary*)
            if not success then failwith "client could not be added"

            //trigger connected
            connected endPoint
            !++ connections
            args.AcceptSocket <- null (*remove the AcceptSocket because we're reusing args*)

            let sd = {Socket = acceptSocket; RemoteEndPoint = endPoint}

            //start receive on accepted client
            //Async.Start(async{let receiveSaea = pool.CheckOut()
            //                  receiveSaea.UserToken <- sd
            //                  acceptSocket.ReceiveAsyncSafe(completed, receiveSaea)})
            let receiveSaea = pool.CheckOut()
            receiveSaea.UserToken <- sd
            acceptSocket.ReceiveAsyncSafe(completed, receiveSaea)

            //check if data was given on connection
            if args.BytesTransferred > 0 then
                let data = acquireData args
                //trigger received
                received (data, s, sd)
        
        | SocketError.OperationAborted
        | SocketError.Disconnecting when disposed -> ()// harmless to stop accepting here, we're being shutdown.
        | _ -> Console.WriteLine (sprintf "socket error on accept: %A" args.SocketError)
         

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
            received (data, s, sd )
            //get on with the next receive
            if socket.Connected then 
                let saea = pool.CheckOut()
                saea.UserToken <- sd
                socket.ReceiveAsyncSafe( completed, saea)
            else ()//? what do we do here?
        else
            //Something went wrong or the client stopped sending bytes.
            disconnect sd

    and processSend (args:SocketAsyncEventArgs) =
        let sd = args.UserToken :?> SocketDescriptor
        match args.SocketError with
        | SocketError.Success ->
            let sentData = acquireData args
            //notify data sent
            sent (sentData, sd.RemoteEndPoint)
        | SocketError.NoBufferSpaceAvailable
        | SocketError.IOPending
        | SocketError.WouldBlock ->
            failwith "Buffer overflow or send buffer timeout" //graceful termination?  
        | _ -> args.SocketError.ToString() |> printfn "socket error on send: %s"

    static member Create(received, ?connected, ?disconnected, ?sent) =
        new TcpServer(5000, 1024, 2000, received, ?connected = connected, ?disconnected = disconnected, ?sent = sent)

    member s.Connections = connections

    ///Starts the accepting a incoming connections.
    member s.Listen(?address, ?port) =
        //if listeningSocket <> null then invalidOp "This server was already started. it is listening on %A." listeningSocket.LocalEndPoint
        let address = defaultArg address IPAddress.Loopback
        let port = defaultArg port 80
        //initialise the pools
        pool.Start(completed)
        ///Creates a Socket and starts listening on the specified address and port.
        listeningSocket.Bind(IPEndPoint(address, port))
        listeningSocket.Listen(acceptBacklogCount)

        listeningSocket.AcceptAsyncSafe(completed, pool.CheckOut())

        { new IDisposable with
            member this.Dispose() = cleanUp listeningSocket }

    ///Sends the specified message to the client.
    member s.Send(clientEndPoint, msg:byte[], ?close) =
        let success, client = clients.TryGetValue(clientEndPoint)
        let close = defaultArg close true
        if success then 
            send {Socket = client;RemoteEndPoint = clientEndPoint}  completed  pool.CheckOut perOperationBufferSize msg close
        else failwith "could not find client %"
        
    interface IDisposable with 
        member s.Dispose() = cleanUp listeningSocket
