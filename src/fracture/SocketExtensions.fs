module Fracture.SocketExtensions
#nowarn "40"

open System
open System.Net
open System.Net.Sockets

/// Helper method to make Async calls easier.  InvokeAsyncMethod ensures the callback always
/// gets called even if an error occurs or the Async method completes synchronously.
let inline private invoke(asyncMethod, callback, args: SocketAsyncEventArgs) =
    if not (asyncMethod args) then callback args

exception SocketIssue of SocketError
    with override this.ToString() = string this.Data0

let inline private invokeAsync(asyncMethod, args: SocketAsyncEventArgs) =
    Async.FromContinuations <| fun (cont,econt,ccont) ->
        let k (args: SocketAsyncEventArgs) =
            match args.SocketError with
            | SocketError.Success -> cont args
            | e -> econt <| SocketIssue e
        let rec finish cont value =
            remover.Dispose()
            cont value
        and remover : IDisposable =
            args.Completed.Subscribe
                ({ new IObserver<_> with
                    member x.OnNext(v) = finish k v
                    member x.OnError(e) = finish econt e
                    member x.OnCompleted() =
                        let msg = "Cancelling the workflow, because the Observable awaited using AwaitObservable has completed."
                        finish ccont (new System.OperationCanceledException(msg)) })
        if not (asyncMethod args) then
            finish k args

type Socket with 
    member s.AcceptAsyncSafe(callback, args) = invoke(s.AcceptAsync, callback, args) 
    member s.ReceiveAsyncSafe(callback, args) = invoke(s.ReceiveAsync, callback, args) 
    member s.SendAsyncSafe(callback, args) = invoke(s.SendAsync, callback, args) 
    member s.ConnectAsyncSafe(callback, args) = invoke(s.ConnectAsync, callback, args)
    member s.DisconnectAsyncSafe(callback, args) = invoke(s.DisconnectAsync, callback, args)

    member s.AsyncAccept(args) = invokeAsync(s.AcceptAsync, args)
    member s.AsyncReceive(args) = invokeAsync(s.ReceiveAsync, args)
    member s.AsyncSend(args) = invokeAsync(s.SendAsync, args)
    member s.AsyncConnect(args) = invokeAsync(s.ConnectAsync, args)
    member s.AsyncDisconnect(args) = invokeAsync(s.DisconnectAsync, args)
