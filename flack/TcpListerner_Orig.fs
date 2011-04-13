module TcpListenerOrig
//    open System
//    open System.Net
//    open System.Net.Sockets
//    open System.Collections.Generic
//    open System.Collections.Concurrent
//    
//    type Socket with 
//        // extension method to make async based call easier, this ensures the callback always gets 
//        // called even if there is an error or the async method completed syncronously
//        member s.InvokeAsyncMethod( asyncmethod, callback, args:SocketAsyncEventArgs) =
//            let result = asyncmethod args
//            if result <> true then callback args
//        member s.AcceptAsyncSafe(callback, args) = s.InvokeAsyncMethod(s.AcceptAsync, callback, args)
//        member s.ReceiveAsyncSafe(callback, args) = s.InvokeAsyncMethod(s.ReceiveAsync, callback, args)
//        member s.SendAsyncSafe(callback, args) = s.InvokeAsyncMethod(s.SendAsync, callback, args)
//        member s.DisconnectAsyncSafe(callback, args) = s.InvokeAsyncMethod(s.DisconnectAsync, callback, args)
//
//    type IPEndPoint with
//        static member Any 
//            with get() =
//                let any = new IPEndPoint(IPAddress.Any, 0)
//                any
//    
//    exception Empty
//    exception Subscript
//
//    type BocketPool( number, size, callback) as this=
//
//        let number = number
//        let size = size
//        let totalsize = (number * size)
//        let buffer = Array.create totalsize 0uy
//        let pool = new BlockingCollection<SocketAsyncEventArgs>(number:int)
//        
//        do
//            let rec loop n =
//                match n with
//                | x when x < totalsize ->
//                    let saea = new SocketAsyncEventArgs()
//                    saea.Completed |> Observable.add( fun saea -> (callback saea))
//                    saea.SetBuffer(buffer, n, size)
//                    this.CheckIn(saea)
//                    loop (n + size)
//                | _ -> ()
//            loop 0                    
//
//        member this.CheckOut()=
//            pool.Take()
//        member this.CheckIn(saea)=
//            pool.Add(saea)
//        member this.Count =
//            pool.Count
//
//    let fillPool (maxinpool, callback) =
//        let pool = new BlockingCollection<SocketAsyncEventArgs>(maxinpool:int)
//        let rec loop n =
//            match n with
//            | x when x < maxinpool -> 
//                let saea = new SocketAsyncEventArgs()
//                saea.Completed |> Observable.add callback 
//                pool.Add saea
//                loop (n+1)
//            | _ -> ()
//        loop 0
//        pool
//
//    type ServerConnection(maxreceives, maxsends, size, socket:Socket) as this =
//        let socket = socket
//        let maxreceives = maxreceives
//        let maxsends = maxsends
//        let sendPool = new BocketPool(maxsends, size, this.receiveCompleted )
//        let receivePool = new BocketPool(maxreceives, size, this.receiveCompleted) 
//
//        member this.Start() = 
//            socket.ReceiveAsyncSafe(this.receiveCompleted, receivePool.CheckOut())
//
//        member this.Stop() =
//            socket.Close(2)
//
//        member this.receiveCompleted (args: SocketAsyncEventArgs) =
//            try
//                match args.LastOperation with //check this is a receive
//                | SocketAsyncOperation.Receive ->
//                    match args.SocketError with
//                    | SocketError.Success ->
//                        //get on with the next receive
//                        socket.ReceiveAsyncSafe( this.receiveCompleted, receivePool.CheckOut())
//                        //process received data
//                        let data = Array.create args.BytesTransferred 0uy
//                        Buffer.BlockCopy(args.Buffer, args.Offset, data, 0, data.Length)
//                        //get the clients endpoint
//                        let client = args.RemoteEndPoint
//                        // remove the endpoint from the saea ,push back
//                        args.RemoteEndPoint <- null
//                        data |> printfn "received data: %A"  
//                        //**TODO: raise event from this component OnDataReceived(data, client);
//                    | _ -> args.SocketError.ToString() |> printfn "socket error on receive: %s"         
//                | _ -> failwith "unknown operation, should be receive"
//            finally
//                receivePool.CheckIn(args)
//                receivePool.Count |> printfn "%d left in receive pool"  
//
//        member this.sendCompleted (args: SocketAsyncEventArgs) =
//            try
//                match args.LastOperation with //check this is a send
//                | SocketAsyncOperation.Send ->
//                    match args.SocketError with
//                    | SocketError.Success ->
//                        //report on the sending...
//                        let sentData = Array.create args.BytesTransferred 0uy
//                        Buffer.BlockCopy(args.Buffer, args.Offset, sentData, 0, sentData.Length);
//                        sentData |> printfn "sent data: %A" 
//                        //get on with the next send
//                        socket.SendAsyncSafe( this.sendCompleted, sendPool.CheckOut())
//                        //**TODO: raise event from this component OnDataSent(data, client);
//                    | _ -> args.SocketError.ToString() |> printfn "socket error on receive: %s"         
//                | _ -> failwith "unknown operation, should be receive"
//            finally
//                sendPool.CheckIn(args)
//                sendPool.Count |> printfn "%d left in send pool"
//
//        member this.Send (msg:byte[]) =
//            let s = sendPool.CheckOut()
//            s.SetBuffer(0, msg.Length)
//            Buffer.BlockCopy(msg, 0, s.Buffer, 0, msg.Length)
//            socket.SendAsyncSafe(this.sendCompleted, s)
//            
//    type TcpListener(maxaccepts, maxsends, maxreceives, size, port, backlog) as this =
//
//        let createUdpSocket() =
//            new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
//
//        let createListener (ip, port, backlog) =
//            let s = createUdpSocket()
//            let ip =
//                match ip with
//                | Some s -> IPAddress.Parse s
//                | None -> IPAddress.Any
//            s.Bind(new IPEndPoint(ip, port))
//            s.Listen(backlog)
//            s
//
//        let listeningSocket = createListener( None, port, backlog)
//
//        let acceptpool = fillPool (maxaccepts, this.acceptcompleted)
//
//        let newserver socket = new ServerConnection (maxreceives, maxsends, size, socket)
//
//        let testMessage = Array.init<byte> 129 (fun _ -> 1uy)
//
//        member this.Clients = new System.Collections.Concurrent.ConcurrentDictionary<IPEndPoint, ServerConnection>(4, 1000)
//
//        member this.acceptcompleted (args : SocketAsyncEventArgs) =
//            try
//                match args.LastOperation with //check this is an accept
//                | SocketAsyncOperation.Accept ->
//                    match args.SocketError with
//                    | SocketError.Success -> 
//                        listeningSocket.AcceptAsyncSafe( this.acceptcompleted, acceptpool.Take())
//                        let newSocket = args.AcceptSocket
//                        //create new serverconnection
//                        let serverConnection = newserver newSocket
//                        //add client to dictionary
//                        let success = this.Clients.TryAdd(args.AcceptSocket.RemoteEndPoint :?> IPEndPoint, serverConnection)
//                        if not success then 
//                            failwith "client could not be added"
//                        else
//                            //start the new connection
//                            serverConnection.Start()
//                        //remove the accept socket from args as we will be reusing it 
//                        args.AcceptSocket <- null 
//                    | _ -> args.SocketError.ToString() |> printfn "socket error on accept: %s"         
//                | _ -> args.LastOperation |> failwith "unknown operation, should be accept but was %a"            
//            finally
//                acceptpool.Add(args)
//                acceptpool.Count |> printfn "%d left in accept pool"
//
//        member this.start () =         
//            listeningSocket.AcceptAsyncSafe( this.acceptcompleted, acceptpool.Take())
//
//        member this.Stop() =
//            listeningSocket.Close()
//
//        member this.Send(client, msg:byte[]) =
//            let success, client = this.Clients.TryGetValue(client)
//            if success then client.Send(msg)
//            else failwith "could not find client %"
//
//    let d = new TcpListener(10, 100, 100, 256, 10003, 1000)
//    try
//        d.start ()
//    with
//    | :? System.Exception as e -> 
//        printfn "%s" e.Message; reraise()
//
//    Console.ReadKey() |> ignore
//    d.Stop()
//    Console.ReadKey() |> ignore