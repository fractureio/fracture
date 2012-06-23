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
open System.Net
open Fracture
open HttpMachine
open System.Collections.Generic
open System.Diagnostics

type HttpRequestHeaders =
    { Method: string
      Uri: string
      Query: string
      Fragment: string
      Version: Version
      KeepAlive: bool
      Headers: IDictionary<string, string> }
    with static member Default =
            { Method = String.Empty
              Uri = String.Empty
              Query = String.Empty
              Fragment = String.Empty
              Version = Version()
              KeepAlive = false
              Headers = new Dictionary<string, string>() :> IDictionary<string, string> }

type HttpRequest =
    { RequestHeaders: HttpRequestHeaders
      Body: ArraySegment<byte> }

type ParserDelegate(onHeaders, requestBody, requestEnded) as p =
    [<DefaultValue>] val mutable method' : string
    [<DefaultValue>] val mutable requestUri: string
    [<DefaultValue>] val mutable fragment : string
    [<DefaultValue>] val mutable queryString : string
    [<DefaultValue>] val mutable headerName : string
    [<DefaultValue>] val mutable headerValue : string
    [<DefaultValue>] val mutable fullRequest:HttpRequest 
    [<DefaultValue>] val mutable requestHeaders:HttpRequestHeaders 
    [<DefaultValue>] val mutable body:ArraySegment<byte>
    let mutable headers = new Dictionary<string,string>()

    let commitHeader() = 
        headers.Add(p.headerName, p.headerValue)
        p.headerName <- null
        p.headerValue <- null

    interface IHttpParserHandler with
        member this.OnMessageBegin(parser: HttpParser) =
            this.method' <- null
            this.requestUri <- null
            this.fragment <- null
            this.queryString <- null
            this.headerName <- null
            this.headerValue <- null
            headers.Clear()

        member this.OnMethod( parser, m) = 
            this.method' <- m

        member this.OnRequestUri(_, requestUri) = 
            this.requestUri <- requestUri

        member this.OnFragment(_, fragment) = 
            this.fragment <- fragment

        member this.OnQueryString(_, queryString) = 
            this.queryString <- queryString

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

            p.requestHeaders <- { Method = this.method'
                                  Uri = this.requestUri
                                  Query = this.queryString
                                  Fragment = this.fragment
                                  Version = Version(parser.MajorVersion, parser.MinorVersion)
                                  KeepAlive = parser.ShouldKeepAlive
                                  Headers = headers }

            onHeaders(p.requestHeaders)

        member this.OnBody(_, data) =
            // XXX can we defer this check to the parser?
            if data.Count > 0 then
                p.body <- data
                requestBody(p.body)

        member this.OnMessageEnd(_) =
            requestEnded({ RequestHeaders = p.requestHeaders; Body = p.body })
