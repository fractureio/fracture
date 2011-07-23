namespace Fracture
open System
open System.Net.Sockets
open System.Collections.Generic
open System.Collections.Concurrent

type internal BocketPool(name, number, size) =
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
            pool.Dispose()

    let time x = 
        let sw = new System.Diagnostics.Stopwatch()
        sw.Start()
        let res =x()
        sw.Stop()
        if sw.ElapsedTicks > 500000L || pool.Count < 10  then
            Console.WriteLine(sprintf "Slow Bocket %s get: %fms, count: %d" name sw.Elapsed.TotalMilliseconds pool.Count)
        res 

    member this.Start(callback) =
        for n in 0 .. number - 1 do
            let saea = new SocketAsyncEventArgs()
            saea.Completed |> Observable.add callback
            saea.SetBuffer(buffer, n*size, size)
            this.CheckIn(saea)

    member this.CheckOut() =
        time pool.Take

    member this.CheckIn(saea) =
        if not disposed then
            // ensure the the full range of the buffer is available this may have changed
            // if the bocket was previously used for a send or connect operation
            if saea.Count < size then 
                saea.SetBuffer(saea.Offset, size)
            pool.Add(saea)

    member this.Count =
        pool.Count

    interface IDisposable with
        member this.Dispose() = cleanUp()
