namespace flack
    open System
    open System.Net
    open System.Net.Sockets
    open System.Collections.Generic
    open System.Collections.Concurrent
    open System.Threading
    open SocketExtensions

    type Connection(maxreceives, maxsends, size, socket:Socket, ?received: byte[] -> unit, ?sent:byte[] -> unit ) as this =
        let socket = socket
        let maxreceives = maxreceives
        let maxsends = maxsends
        let sendPool = new BocketPool(maxsends, size, this.sendCompleted )
        let receivePool = new BocketPool(maxreceives, size, this.receiveCompleted) 
        let mutable disposed = false
        let mutable anyErrors = false

        let runIfSome a b =
            match b with
            | Some(b) -> a |> b
            | None -> ()

        let ( |?> ) = runIfSome

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
        
        member private this.receiveCompleted (args: SocketAsyncEventArgs) =
            try
                match args.LastOperation with
                | SocketAsyncOperation.Receive ->
                    match args.SocketError with
                    | SocketError.Success ->
                        //get on with the next receive
                        socket.ReceiveAsyncSafe( this.receiveCompleted, receivePool.CheckOut())
                        //process received data
                        let data = Array.create args.BytesTransferred 0uy
                        Buffer.BlockCopy(args.Buffer, args.Offset, data, 0, data.Length)
                        //get the clients endpoint
                        let client = args.RemoteEndPoint
                        // remove the endpoint from the saea ,push back
                        args.RemoteEndPoint <- null
                        //notify data received
                        data |?> received 
                    | _ -> args.SocketError.ToString() |> printfn "socket error on receive: %s"         
                | _ -> failwith "unknown operation, should be receive"
            finally
                receivePool.CheckIn(args)
        
        member private this.sendCompleted (args: SocketAsyncEventArgs) =
            try
                match args.LastOperation with
                | SocketAsyncOperation.Send ->
                    match args.SocketError with
                    | SocketError.Success ->
                         //report on the sending...
                          let sentData = Array.create args.BytesTransferred 0uy
                          Buffer.BlockCopy(args.Buffer, args.Offset, sentData, 0, sentData.Length);
                          //notify data sent
                          sentData |?> sent
                          //get on with the next send
                          socket.SendAsyncSafe( this.sendCompleted, sendPool.CheckOut())

                    | SocketError.NoBufferSpaceAvailable
                    | SocketError.IOPending
                    | SocketError.WouldBlock ->
                        if not(anyErrors) then
                            anyErrors <- true
                            failwith "Buffer overflow or send buffer timeout"   
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

