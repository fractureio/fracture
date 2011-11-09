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

    let cleanUp disposing = 
        if not disposed then
            if disposing then
                pool.CompleteAdding()
                while pool.Count > 1 do
                    pool.Take()
                        .Dispose()
                pool.Dispose()
            disposed <- true

    let checkedOperation operation onFailure =
        try 
            operation()
        with
            | :? ArgumentNullException
            | :? InvalidOperationException -> onFailure()

    let raiseDisposed() = raise(ObjectDisposedException(name))

    member this.Start(callback) =
        for n in 0 .. maxPoolCount - 1 do
            let args = new SocketAsyncEventArgs()
            args.Completed |> Observable.add callback
            args.SetBuffer(buffer, n*perBocketBufferSize, perBocketBufferSize)
            this.CheckIn(args)

    member this.CheckOut() =
        if not disposed then
            checkedOperation pool.Take raiseDisposed
        else raiseDisposed()

    member this.CheckIn(args) =
        if not disposed then
            // ensure the the full range of the buffer is available this may have changed
            // if the bocket was previously used for a send or connect operation.
            if args.Count < perBocketBufferSize then 
                args.SetBuffer(args.Offset, perBocketBufferSize)
            // we might be trying to update the the pool when it's already been disposed. 
            checkedOperation (fun () -> pool.Add(args)) args.Dispose
        // the pool is kicked, dispose of it ourselves.
        else args.Dispose()
            
    member this.Count = pool.Count

    member this.Dispose = (this :> IDisposable).Dispose()

    override this.Finalize() = cleanUp false

    interface IDisposable with
        member this.Dispose() =
            cleanUp true
            GC.SuppressFinalize(this)