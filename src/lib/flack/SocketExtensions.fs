module SocketExtensions
    open System
    open System.Net
    open System.Net.Sockets

    /// helper method to make async based call easier, this ensures the callback always gets 
    /// called even if there is an error or the async method completed syncronously
    let InvokeAsyncMethod( asyncmethod, callback, args:SocketAsyncEventArgs) =
           let result = asyncmethod args
           if result <> true then callback args

    type Socket with 

        member s.AcceptAsyncSafe(callback, args) = 
            InvokeAsyncMethod(s.AcceptAsync, callback, args ) 

        member s.ReceiveAsyncSafe(callback, args) = 
            InvokeAsyncMethod(s.ReceiveAsync,callback, args) 

        member s.SendAsyncSafe(callback, args) = 
            InvokeAsyncMethod(s.SendAsync,callback, args) 

        member s.ConnectAsyncSafe(callback, args) = 
            InvokeAsyncMethod(s.ConnectAsync,callback, args )

        member s.DisconnectAsyncSafe(callback, args) = 
            InvokeAsyncMethod( s.DisconnectAsync, callback, args)