module SocketExtensions
    open System
    open System.Net
    open System.Net.Sockets

    /// helper method to make async based call easier, this ensures the callback always gets 
    /// called even if there is an error or the async method completed syncronously
    let invokesafe callback (args:SocketAsyncEventArgs) asyncmethod =
        let result = asyncmethod args
        if result <> true then callback args

    type Socket with 
        member s.AcceptAsyncSafe(callback, args) = 
            invokesafe callback  args  s.AcceptAsync

        member s.ReceiveAsyncSafe(callback, args) = 
            invokesafe callback, args, s.ReceiveAsync

        member s.SendAsyncSafe(callback, args) = 
            invokesafe callback args s.SendAsync

        member s.ConnectAsyncSafe(callback, args) = 
            invokesafe callback args s.ConnectAsync

        member s.DisconnectAsyncSafe(callback, args) = 
            invokesafe callback args s.DisconnectAsync