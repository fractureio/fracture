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