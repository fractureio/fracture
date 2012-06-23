//----------------------------------------------------------------------------
//
// Copyright (c) 2011-2012 Dave Thomas (@7sharp9) 
//                         Ryan Riley (@panesofglass)
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
module Fracture.SocketExtensions
#nowarn "40"

open System
open System.Net
open System.Net.Sockets

/// Helper method to make Async calls easier.  InvokeAsyncMethod ensures the callback always
/// gets called even if an error occurs or the Async method completes synchronously.
let inline private invoke(asyncMethod, callback, args: SocketAsyncEventArgs) =
    if not (asyncMethod args) then callback args

type Socket with 
    member s.AcceptAsyncSafe(callback, args) = invoke(s.AcceptAsync, callback, args) 
    member s.ReceiveAsyncSafe(callback, args) = invoke(s.ReceiveAsync, callback, args) 
    member s.SendAsyncSafe(callback, args) = invoke(s.SendAsync, callback, args) 
    member s.ConnectAsyncSafe(callback, args) = invoke(s.ConnectAsync, callback, args)
    member s.DisconnectAsyncSafe(callback, args) = invoke(s.DisconnectAsync, callback, args)
