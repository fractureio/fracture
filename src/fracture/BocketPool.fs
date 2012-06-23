//----------------------------------------------------------------------------
//
// Copyright (c) 2011-2012 Dave Thomas (@7sharp9) Ryan Riley (@panesofglass)
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//----------------------------------------------------------------------------
namespace Fracture

open System
open System.Net.Sockets
open System.Collections.Generic
open System.Collections.Concurrent
open SocketExtensions
open Microsoft.FSharp.Core.Operators.Unchecked

type internal BocketPool(name, maxPoolCount, perBocketBufferSize) =
    let totalsize = (maxPoolCount * perBocketBufferSize)
    let buffer = Array.zeroCreate<byte> totalsize
    let pool = new BlockingCollection<SocketAsyncEventArgs>(maxPoolCount:int)
    let subscriptions = new ConcurrentDictionary<int, IDisposable>()
    let disposed = ref false

    let dispose (args: SocketAsyncEventArgs) =
        let subscription = subscriptions.[args.GetHashCode()]
        subscription.Dispose()
        args.Dispose()

    let cleanUp disposing = 
        if not !disposed then
            if disposing then
                pool.CompleteAdding()
                while pool.Count > 1 do
                    let args = pool.Take()
                    dispose args
                pool.Dispose()
            disposed := true

    let checkedOperation operation onFailure =
        try 
            operation()
        with
        | :? ArgumentNullException
        | :? InvalidOperationException -> onFailure()

    let raiseDisposed() = raise(ObjectDisposedException(name))
    let raiseTimeout() = raise(TimeoutException(name))

    static member inline TryTakeAsTuple (pool: BlockingCollection<_>) (timeout:int)  = 
        let result = ref defaultof< 'a>
        let success = pool.TryTake(result, timeout)
        (success, result)

    member this.Start(callback) =
        for n in 0 .. maxPoolCount - 1 do
            let args = new SocketAsyncEventArgs()
            args.DisconnectReuseSocket <- true
            let subscription = args.Completed |> Observable.subscribe callback
            subscriptions.[args.GetHashCode()] <- subscription
            args.SetBuffer(buffer, n*perBocketBufferSize, perBocketBufferSize)
            this.CheckIn(args)

    member this.CheckOut() =
        if not !disposed then
            let suc,res = BocketPool.TryTakeAsTuple pool 10000
            if suc then 
                res.Value 
            else raiseTimeout()
            //checkedOperation pool.Take raiseDisposed
        else raiseDisposed()

    member this.CheckIn(args) =
        if not !disposed then
            // ensure the the full range of the buffer is available this may have changed
            // if the bocket was previously used for a send or connect operation.
            if args.Count < perBocketBufferSize then 
                args.SetBuffer(args.Offset, perBocketBufferSize)
            // we might be trying to update the the pool when it's already been disposed. 
            checkedOperation (fun () -> pool.Add(args)) (fun () -> dispose args)
        // the pool is kicked, dispose of it ourselves.
        else dispose args
            
    member this.Count = pool.Count

    member this.Dispose() = (this :> IDisposable).Dispose()

    override this.Finalize() = cleanUp false

    interface IDisposable with
        member this.Dispose() =
            cleanUp true
            GC.SuppressFinalize(this)
