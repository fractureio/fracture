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
namespace Fracture.Http

open System
open System.Collections.Generic
open System.Net
open System.Text
open Fracture
open FSharp.Control
open HttpMachine
open Owin

type ParserDelegate(app, send: bool -> byte[] -> unit, ?onHeaders, ?requestBody, ?requestEnded) as p =
    [<DefaultValue>] val mutable httpMethod : string
    [<DefaultValue>] val mutable headerName : string
    [<DefaultValue>] val mutable headerValue : string
    [<DefaultValue>] val mutable onBody : ReplaySubject<ArraySegment<byte>>
    [<DefaultValue>] val mutable request : Owin.Request
    [<DefaultValue>] val mutable finished : bool

    let commitHeader() = 
        p.request.Headers.[p.headerName] <- [|p.headerValue|]
        p.headerName <- null
        p.headerValue <- null

    let toHttpStatusCode (i:int) = Enum.ToObject(typeof<HttpStatusCode>, i)

    let responseToBytes (res: Response) = 
        let headers =
            String.Join(
                "\r\n",
                [|  yield sprintf "HTTP/1.1 %i %A" res.StatusCode <| Enum.ToObject(typeof<HttpStatusCode>, res.StatusCode)
                    for KeyValue(header, values) in res.Headers do
                        // TODO: Fix handling of certain headers where this approach is invalid, e.g. Set-Cookie
                        yield sprintf "%s: %s" header <| String.Join(",", values)
                    // Add the body separator.
                    yield "\r\n"
                |])
            |> Encoding.ASCII.GetBytes

        asyncSeq {
            yield ArraySegment<_>(headers)
            yield! res.Body
        }

    let run app req keepAlive = async {
        let! res = app req
        for line : ArraySegment<_> in responseToBytes res do
            send keepAlive line.Array.[line.Offset..(line.Offset + line.Count - 1)]
    }

    interface IHttpParserHandler with
        member this.OnMessageBegin(parser: HttpParser) =
            this.finished <- false
            this.httpMethod <- null
            this.headerName <- null
            this.headerValue <- null
            this.onBody <- ReplaySubject<ArraySegment<byte>>(32) // TODO: Determine the correct buffer size.
            this.request <- {
                Environment = new Dictionary<string, obj>()
                Headers = new Dictionary<string, string[]>()
                Body = this.onBody |> AsyncSeq.ofObservableBuffered
            }
            this.request.Environment.Add(Request.Version, "1.0")

        member this.OnMethod( parser, m) = 
            this.httpMethod <- m
            this.request.Environment.Add(Request.Method, m)

        member this.OnRequestUri(_, requestUri) = 
            let uri = Uri(requestUri, UriKind.RelativeOrAbsolute)
            this.request.Environment.Add("fracture.RequestUri", uri)

            // TODO: Fix this so that the path can be determined correctly.
            this.request.Environment.Add(Request.PathBase, "")

            if uri.IsAbsoluteUri then
                this.request.Environment.Add(Request.Path, uri.AbsolutePath)
                this.request.Environment.Add(Request.Scheme, uri.Scheme)
                this.request.Headers.Add("Host", [|uri.Host|])
            else
                this.request.Environment.Add(Request.Path, requestUri)

        member this.OnFragment(_, fragment) = 
            this.request.Environment.Add("fracture.RequestFragment", fragment)

        member this.OnQueryString(_, queryString) = 
            this.request.Environment.Add(Request.QueryString, queryString)

        member this.OnHeaderName(_, name) = 
            if not (String.IsNullOrEmpty(this.headerValue)) then
                commitHeader()
            this.headerName <- name

        member this.OnHeaderValue(_, value) = 
            if String.IsNullOrEmpty(this.headerName) then
                failwith "Got a header value without name."
            this.headerValue <- value

        member this.OnHeadersEnd(parser) = 
            if not (String.IsNullOrEmpty(this.headerValue)) then
                commitHeader()

            this.request.Environment.Add("fracture.HttpVersion", Version(parser.MajorVersion, parser.MinorVersion))

            Async.StartWithContinuations(
                run app this.request parser.ShouldKeepAlive,
                (fun _ -> send parser.ShouldKeepAlive Array.empty |> ignore),
                ignore, ignore)

            // NOTE: This isn't technically correct as a body with these methods is undefined, not unallowed.
            if this.httpMethod = "GET" || this.httpMethod = "HEAD" then
                this.onBody.OnCompleted()
                this.finished <- true

            onHeaders |> Option.iter (fun f -> f this.request)

        member this.OnBody(_, data) =
            // XXX can we defer this check to the parser?
            if data.Count > 0 && not this.finished then
                this.onBody.OnNext data
                requestBody |> Option.iter (fun f -> f data)

        member this.OnMessageEnd(_) =
            if not this.finished then
                this.onBody.OnCompleted()
                this.finished <- true
            requestEnded |> Option.iter (fun f -> f this.request)
