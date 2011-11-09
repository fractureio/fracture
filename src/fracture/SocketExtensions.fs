module Fracture.SocketExtensions

open System
open System.Net
open System.Net.Sockets

/// Helper method to make Async calls easier.  InvokeAsyncMethod ensures the callback always
/// gets called even if an error occurs or the Async method completes synchronously.
let inline private invoke(asyncMethod, callback, args: SocketAsyncEventArgs) =
    if not (asyncMethod args) then callback args

type Socket with 
    member s.AcceptAsyncSafe(callback, args) =
        invoke(s.AcceptAsync, callback, args) 
    member s.ReceiveAsyncSafe(callback, args) =
        invoke(s.ReceiveAsync, callback, args) 
    member s.SendAsyncSafe(callback, args) =
        invoke(s.SendAsync, callback, args) 
    member s.ConnectAsyncSafe(callback, args) =
        invoke(s.ConnectAsync, callback, args)
    member s.DisconnectAsyncSafe(callback, args) =
        invoke(s.DisconnectAsync, callback, args)
