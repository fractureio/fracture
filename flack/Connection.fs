namespace flack
    open System
    open System.Net
    open System.Net.Sockets
    open System.Collections.Generic
    open System.Collections.Concurrent
    open System.Threading
    open SocketExtensions

    type internal Connection(maxreceives, maxsends, size, socket, disconnected, sent, received ) as this =
        let socket:Socket = socket
        let maxreceives = maxreceives
        let maxsends = maxsends
        let sendPool = new BocketPool(maxsends, size, this.sendCompleted )
        let receivePool = new BocketPool(maxreceives, size, this.receiveCompleted) 
        let mutable disposed = false

        let cleanUp() = 
            if not disposed then
                disposed <- true
                socket.Shutdown(SocketShutdown.Both)
                socket.Disconnect(false)
                socket.Close()
                (sendPool :> IDisposable).Dispose()
                (receivePool :> IDisposable).Dispose()
        
        member this.Start() = 
            socket.ReceiveAsyncSafe(this.receiveCompleted, receivePool.CheckOut())

        member this.Stop() =
            socket.Close(2)
        
        member this.receiveCompleted (args: SocketAsyncEventArgs) =
            try
                match args.LastOperation with
                | SocketAsyncOperation.Receive ->
                    match args.SocketError with
                    | SocketError.Success ->
                        //get on with the next receive
                        socket.ReceiveAsyncSafe( this.receiveCompleted, receivePool.CheckOut())
                        //process received data
                        let data:byte[] = Array.zeroCreate args.BytesTransferred
                        Buffer.BlockCopy(args.Buffer, args.Offset, data, 0, data.Length)
                        //notify data received
                        (data, socket.RemoteEndPoint :?> IPEndPoint) |> received 
                    | _ -> 
                        socket.RemoteEndPoint :?> IPEndPoint |> disconnected
                | _ -> failwith "unknown operation, should be receive"
            finally
                receivePool.CheckIn(args)
        
        member this.sendCompleted (args: SocketAsyncEventArgs) =
            try
                match args.LastOperation with
                | SocketAsyncOperation.Send ->
                    match args.SocketError with
                    | SocketError.Success ->
                          let sentData:byte[] = Array.zeroCreate args.BytesTransferred
                          Buffer.BlockCopy(args.Buffer, args.Offset, sentData, 0, sentData.Length);
                          //notify data sent
                          (sentData, socket.RemoteEndPoint :?> IPEndPoint) |> sent
                          //on with the next send
                          socket.SendAsyncSafe( this.sendCompleted, sendPool.CheckOut())
                    | SocketError.NoBufferSpaceAvailable
                    | SocketError.IOPending
                    | SocketError.WouldBlock ->
                        failwith "Buffer overflow or send buffer timeout" //graceful termination?  
                    | _ -> args.SocketError.ToString() |> printfn "socket error on send: %s"         
                | _ -> failwith "invalid operation, should be receive"
            finally
                sendPool.CheckIn(args)

        member this.Send (msg:byte[]) =
            let s = sendPool.CheckOut()
            Buffer.BlockCopy(msg, 0, s.Buffer, s.Offset, msg.Length)
            socket.SendAsyncSafe(this.sendCompleted, s)

        interface IDisposable with
            member this.Dispose() = cleanUp()