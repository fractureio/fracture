namespace flack
    open System
    open System.Net
    open System.Net.Sockets
    open System.Collections.Generic
    open System.Collections.Concurrent
    open SocketExtensions

    type Callbacks = {
        Connected: IPEndPoint -> unit; 
        Disconnected: IPEndPoint -> unit; 
        Sent: byte[] * IPEndPoint -> unit; 
        Received:byte[] * IPEndPoint -> unit}