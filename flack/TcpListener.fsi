namespace flack
  type TcpListener =
    class
      interface System.IDisposable
      new : port:int -> TcpListener
      new : maxaccepts:int * maxsends:int * maxreceives:int * size:int *
            port:int * backlog:int -> TcpListener
      member Close : unit -> unit
      member Send : client:System.Net.IPEndPoint * msg:byte [] -> unit
      member Start : unit -> unit
      member
        private acceptcompleted : args:System.Net.Sockets.SocketAsyncEventArgs ->
                                    unit
      member add_Connected : Handler<System.Net.IPEndPoint> -> unit
      member add_Disconnected : Handler<System.Net.IPEndPoint> -> unit
      member add_Received : Handler<byte [] * System.Net.IPEndPoint> -> unit
      member add_Sent : Handler<byte [] * System.Net.IPEndPoint> -> unit
      [<CLIEventAttribute ()>]
      member Connected : IEvent<System.Net.IPEndPoint>
      [<CLIEventAttribute ()>]
      member Disconnected : IEvent<System.Net.IPEndPoint>
      [<CLIEventAttribute ()>]
      member Received : IEvent<byte [] * System.Net.IPEndPoint>
      [<CLIEventAttribute ()>]
      member Sent : IEvent<byte [] * System.Net.IPEndPoint>
      member remove_Connected : Handler<System.Net.IPEndPoint> -> unit
      member remove_Disconnected : Handler<System.Net.IPEndPoint> -> unit
      member remove_Received : Handler<byte [] * System.Net.IPEndPoint> -> unit
      member remove_Sent : Handler<byte [] * System.Net.IPEndPoint> -> unit
    end