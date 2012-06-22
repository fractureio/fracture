namespace Owin

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open FSharp.Control

type MessageBody = AsyncSeq<ArraySegment<byte>>

[<Struct>]
type Request =
    val Environment : IDictionary<string, obj>
    val Headers : IDictionary<string, string[]>
    val Body : MessageBody

[<Struct>]
type Response =
    val StatusCode : int
    val Headers : IDictionary<string, string[]>
    val Body : MessageBody
    val Properties : IDictionary<string, obj>

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
