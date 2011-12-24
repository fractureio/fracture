namespace Fracture

open System
open System.Diagnostics
open System.Net
open System.Net.Sockets
open System.Collections.Generic
open System.Collections.Concurrent
open Fracture.SocketExtensions
open Fracture.Pipelets
open System.Threading.Tasks.Dataflow
open System.Threading
open Fracture.Common
open Fracture.Threading

///Creates a new TcpServer using the specified parameters
type TcpServer(poolSize, perOperationBufferSize, acceptBacklogCount, received, ?connected, ?disconnected, ?sent, ?overflow)=
    let pool = new BocketPool("regular pool", max poolSize 2, perOperationBufferSize)
    let connectionPool = new BocketPool("connection pool", max acceptBacklogCount 2, max perOperationBufferSize 288)(*Note: 288 bytes is the minimum size for a connection*)
    let clients = new ConcurrentDictionary<_,_>()
    let connections = ref 0
    let listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    let mutable disposed = false
    let errors (msg:string) = Console.WriteLine(msg)
    let receiveAction = new ActionBlock<_>(received)

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

    let propagateReceive (receiveArgs)=
        if not <|receiveAction.Post(receiveArgs) then
            overflow |> Option.iter  (fun overflow-> overflow receiveArgs)

    ///This function is called on each connect,sends,receive, and disconnect
    let rec completed(args:SocketAsyncEventArgs) =
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
            if not (args.LastOperation = SocketAsyncOperation.Accept) then
                pool.CheckIn(args)//only check in non accepts
    
    and processAccept(args) =
        match args.SocketError with
        | SocketError.Success ->
            let acceptSocket = args.AcceptSocket
            let endPoint = acceptSocket.RemoteEndPoint

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
                propagateReceive(endPoint, data)

            //start next accept
            listeningSocket.AcceptAsyncSafe(completed, args)
        
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
            propagateReceive(endPoint, data)
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
    
    let sendAction = new ActionBlock<_>(fun (endPoint, msg, keepAlive) ->
        let foundclient, client = clients.TryGetValue(endPoint)
        if foundclient then
            if client.Connected then
                try send client endPoint completed msg keepAlive pool.CheckOut (defaultArg disconnected ignore)
                with
                | ex -> errors ex.Message
            else errors <| String.Format("Not sending, client:{0} not connected", endPoint.ToString() )
        else errors <| String.Format("could not find client:{0}", endPoint.ToString()) )

    /// PoolSize=50k, Per operation buffer=1k, accept backlog=1000
    static member Create(received, ?connected, ?disconnected, ?sent) =
        new TcpServer(50000, 1024, 1000, received, ?connected = connected, ?disconnected = disconnected, ?sent = sent)

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

    member s.Dispose() = (s :> IDisposable).Dispose()
    
    member s.Send endPoint keepAlive msg = 
        sendAction.Post( (endPoint, msg, keepAlive) )

    interface IDisposable with 
        member s.Dispose() =
            cleanUp true
            GC.SuppressFinalize(s)
