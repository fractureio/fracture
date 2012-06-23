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
open Fracture
open FSharp.Control
open HttpMachine
open Owin

type ParserDelegate(?onHeaders, ?requestBody, ?requestEnded) as p =
    [<DefaultValue>] val mutable headerName : string
    [<DefaultValue>] val mutable headerValue : string
    [<DefaultValue>] val mutable onBody: Event<ArraySegment<byte>>
    [<DefaultValue>] val mutable request: Owin.Request

    let commitHeader() = 
        p.request.Headers.[p.headerName] <- [|p.headerValue|]
        p.headerName <- null
        p.headerValue <- null

    interface IHttpParserHandler with
        member this.OnMessageBegin(parser: HttpParser) =
            this.headerName <- null
            this.headerValue <- null
            this.onBody <- Event<ArraySegment<byte>>()
            this.request <- {
                Environment = new Dictionary<string, obj>()
                Headers = new Dictionary<string, string[]>()
                Body = this.onBody.Publish |> AsyncSeq.ofObservableBuffered
            }
            this.request.Environment.Add(Request.Version, "1.0")

        member this.OnMethod( parser, m) = 
            this.request.Environment.Add(Request.Method, m)

        member this.OnRequestUri(_, requestUri) = 
            let uri = Uri(requestUri)
            this.request.Environment.Add("fracture.RequestUri", uri)

            // TODO: Fix this so that the path can be determined correctly.
            this.request.Environment.Add(Request.PathBase, "")
            this.request.Environment.Add(Request.Path, uri.AbsolutePath)

            if uri.IsAbsoluteUri then
                this.request.Environment.Add(Request.Scheme, uri.Scheme)
                this.request.Headers.Add("Host", [|uri.Host|])

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
            this.request.Environment.Add("fracture.KeepAlive", parser.ShouldKeepAlive)

            onHeaders |> Option.iter (fun f -> f this.request)

        member this.OnBody(_, data) =
            // XXX can we defer this check to the parser?
            if data.Count > 0 then
                this.onBody.Trigger data
                requestBody |> Option.iter (fun f -> f data)

        member this.OnMessageEnd(_) =
            requestEnded |> Option.iter (fun f -> f this.request)
