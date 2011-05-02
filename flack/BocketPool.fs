namespace flack
    open System
    open System.Net.Sockets
    open System.Collections.Concurrent

    type internal BocketPool(number, size) =
        let totalsize = (number * size)
        let buffer = Array.zeroCreate<byte> totalsize
        let pool = new BlockingCollection<SocketAsyncEventArgs>(number:int)
        let mutable disposed = false
        let cleanUp() = 
            if not disposed then
                disposed <- true
                pool.CompleteAdding()
                while pool.Count > 1 do
                    pool.Take()
                        .Dispose()

        member this.Start(callback) =
            let rec loop n =
                if n < number then
                    let saea = new SocketAsyncEventArgs()
                    saea.Completed |> Observable.add callback
                    saea.SetBuffer(buffer, n, size)
                    this.CheckIn(saea)
                    loop (n + 1)
            loop 0                    
        member this.CheckOut() =
            pool.Take()
        member this.CheckIn(saea) =
            //ensure the the full range of the buffer is available
            //this may have changed if the bocket was previously
            //used for a send or connect operation
            if saea.Count < size then 
                saea.SetBuffer(saea.Offset, size)
            pool.Add(saea)
        member this.Count =
            pool.Count
        interface IDisposable with
            member this.Dispose() = cleanUp()
