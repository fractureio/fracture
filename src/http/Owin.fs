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
namespace Owin

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open FSharp.Control

type MessageBody = AsyncSeq<ArraySegment<byte>>

type Request = {
    Environment : IDictionary<string, obj>
    Headers : IDictionary<string, string[]>
    Body : MessageBody
}

type Response = {
    StatusCode : int
    Headers : IDictionary<string, string[]>
    Body : MessageBody
    Properties : IDictionary<string, obj>
}

type AppDelegate = Request -> Async<Response>

[<Interface>]
type IAppBuilder =
    abstract member Use : 'a -> 'a -> IAppBuilder
    abstract member Build : IAppBuilder -> unit
    abstract member AddAdapters : ('a -> 'b) * ('b -> 'a) -> IAppBuilder
    abstract member Properties : IDictionary<string, obj> with get

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Request =
    let Version = "owin.Version"
    let Scheme = "owin.RequestScheme"
    let Method = "owin.RequestMethod"
    let PathBase = "owin.RequestPathBase"
    let Path = "owin.RequestPath"
    let QueryString = "owin.RequestQueryString"
    let Headers = "owin.RequestHeaders"
    let Body = "owin.RequestBody"
