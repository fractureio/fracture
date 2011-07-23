module Fracture.SocketExtensions

open System
open System.Net
open System.Net.Sockets

/// Helper method to make Async calls easier.
/// invokeAsyncMethod ensures the callback always gets called,
/// even if an error occurs or the Async method completes synchronously.
let invokeAsyncMethod( asyncmethod, callback, args:SocketAsyncEventArgs) =
    if asyncmethod args then 
        callback args

type Socket with 
    member s.AcceptAsyncSafe(callback, args) =  invokeAsyncMethod(s.AcceptAsync, callback, args) 
    member s.ReceiveAsyncSafe(callback, args) =  invokeAsyncMethod(s.ReceiveAsync,callback, args) 
    member s.SendAsyncSafe(callback, args) =  invokeAsyncMethod(s.SendAsync,callback, args) 
    member s.ConnectAsyncSafe(callback, args) =  invokeAsyncMethod(s.ConnectAsync,callback, args)
    member s.DisconnectAsyncSafe(callback, args) =  invokeAsyncMethod(s.DisconnectAsync, callback, args)