﻿namespace Fracture
open System
open System.Net.Sockets
open System.Collections.Generic
open System.Collections.Concurrent

type internal BocketPool(name, maxPoolCount, perBocketBufferSize) =
    let totalsize = (maxPoolCount * perBocketBufferSize)
    let buffer = Array.zeroCreate<byte> totalsize
    let pool = new BlockingCollection<SocketAsyncEventArgs>(maxPoolCount:int)
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
        if sw.ElapsedTicks > 500000L then
            sprintf "Slow Bocket %s get: %fms, count: %d" name sw.Elapsed.TotalMilliseconds pool.Count |> Common.logger
        res 

    let checkedOperation operation onFailure =
        try 
            operation()
        with
            | :? ArgumentNullException
            | :? InvalidOperationException -> onFailure()

    let raiseDisposed() = raise(ObjectDisposedException(name))

    member this.Start(callback) =
        for n in 0 .. maxPoolCount - 1 do
            let saea = new SocketAsyncEventArgs()
            saea.Completed |> Observable.add callback
            saea.SetBuffer(buffer, n*perBocketBufferSize, perBocketBufferSize)
            this.CheckIn(saea)

    member this.CheckOut() =
        if disposed then
            raiseDisposed()
        else
            checkedOperation pool.Take raiseDisposed

    member this.CheckIn(saea) =
        if disposed then
            // the pool is kicked; let's just dispose of it ourselves
            saea.Dispose()
        else 
            // ensure the the full range of the buffer is available this may have changed
            // if the bocket was previously used for a send or connect operation
            if saea.Count < perBocketBufferSize then 
                saea.SetBuffer(saea.Offset, perBocketBufferSize)
            // we might be trying to update the the pool when it's already been disposed 
            checkedOperation (fun () -> pool.Add(saea)) saea.Dispose
            
    member this.Count =
        pool.Count

    interface IDisposable with
        member this.Dispose() = cleanUp()
