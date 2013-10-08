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
open System.Threading
open Fracture
open HttpMachine
open Owin

/// An Environment dictionary to store OWIN request and response values.
type internal ServerEnvironment() as x =
    inherit Environment(new Dictionary<_,_>())

    (* Set environment settings *)

    // Set a per-request cancellation token
    // TODO: Determine if this can use the token from the Async block.
    let cts = new CancellationTokenSource()
    do x.Add(Constants.callCancelled, cts.Token)

    do x.Add(Constants.owinVersion, "1.0")

    (* Set request defaults *)

    // Add the request headers dictionary
    let requestHeaders = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    do x.Add(Constants.requestHeaders, requestHeaders)

    let requestBody = new IO.MemoryStream()
    do x.Add(Constants.requestBody, requestBody)

    (* Set response defaults *)
    do x.Add(Constants.responseStatusCode, 404)

    // Add the response headers dictionary
    let responseHeaders = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    do x.Add(Constants.responseHeaders, responseHeaders)

    let responseBody = new IO.MemoryStream()
    do x.Add(Constants.responseBody, responseBody)

    /// Gets the request headers dictionary for the current request.
    override x.RequestHeaders = requestHeaders :> _

    /// Gets the request body for the current request.
    override x.RequestBody = requestBody :> _

    /// Gets the response headers dictionary for the current response.
    override x.ResponseHeaders = responseHeaders :> _

    /// Gets the response body stream.
    override x.ResponseBody = responseBody :> _

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Response =
    open System.Net
    open System.Text

    let BS input =
        ArraySegment<byte>(input)

    [<CompiledName("GetStatusLine")>]
    let getStatusLine = function
      | 100 -> BS"HTTP/1.1 100 Continue\r\n"B
      | 101 -> BS"HTTP/1.1 101 Switching Protocols\r\n"B
      | 102 -> BS"HTTP/1.1 102 Processing\r\n"B
      | 200 -> BS"HTTP/1.1 200 OK\r\n"B
      | 201 -> BS"HTTP/1.1 201 Created\r\n"B
      | 202 -> BS"HTTP/1.1 202 Accepted\r\n"B
      | 203 -> BS"HTTP/1.1 203 Non-Authoritative Information\r\n"B
      | 204 -> BS"HTTP/1.1 204 No Content\r\n"B
      | 205 -> BS"HTTP/1.1 205 Reset Content\r\n"B
      | 206 -> BS"HTTP/1.1 206 Partial Content\r\n"B
      | 207 -> BS"HTTP/1.1 207 Multi-Status\r\n"B
      | 208 -> BS"HTTP/1.1 208 Already Reported\r\n"B
      | 226 -> BS"HTTP/1.1 226 IM Used\r\n"B
      | 300 -> BS"HTTP/1.1 300 Multiple Choices\r\n"B
      | 301 -> BS"HTTP/1.1 301 Moved Permanently\r\n"B
      | 302 -> BS"HTTP/1.1 302 Found\r\n"B
      | 303 -> BS"HTTP/1.1 303 See Other\r\n"B
      | 304 -> BS"HTTP/1.1 304 Not Modified\r\n"B
      | 305 -> BS"HTTP/1.1 305 Use Proxy\r\n"B
      | 306 -> BS"HTTP/1.1 306 Switch Proxy\r\n"B
      | 307 -> BS"HTTP/1.1 307 Temporary Redirect\r\n"B
      | 308 -> BS"HTTP/1.1 308 Permanent Redirect\r\n"B
      | 400 -> BS"HTTP/1.1 400 Bad Request\r\n"B
      | 401 -> BS"HTTP/1.1 401 Unauthorized\r\n"B
      | 402 -> BS"HTTP/1.1 402 Payment Required\r\n"B
      | 403 -> BS"HTTP/1.1 403 Forbidden\r\n"B
      | 404 -> BS"HTTP/1.1 404 Not Found\r\n"B
      | 405 -> BS"HTTP/1.1 405 Method Not Allowed\r\n"B
      | 406 -> BS"HTTP/1.1 406 Not Acceptable\r\n"B
      | 407 -> BS"HTTP/1.1 407 Proxy Authentication Required\r\n"B
      | 408 -> BS"HTTP/1.1 408 Request Timeout\r\n"B
      | 409 -> BS"HTTP/1.1 409 Conflict\r\n"B
      | 410 -> BS"HTTP/1.1 410 Gone\r\n"B
      | 411 -> BS"HTTP/1.1 411 Length Required\r\n"B
      | 412 -> BS"HTTP/1.1 412 Precondition Failed\r\n"B
      | 413 -> BS"HTTP/1.1 413 Request Entity Too Large\r\n"B
      | 414 -> BS"HTTP/1.1 414 Request-URI Too Long\r\n"B
      | 415 -> BS"HTTP/1.1 415 Unsupported Media Type\r\n"B
      | 416 -> BS"HTTP/1.1 416 Request Range Not Satisfiable\r\n"B
      | 417 -> BS"HTTP/1.1 417 Expectation Failed\r\n"B
      | 418 -> BS"HTTP/1.1 418 I'm a teapot\r\n"B
      | 422 -> BS"HTTP/1.1 422 Unprocessable Entity\r\n"B
      | 423 -> BS"HTTP/1.1 423 Locked\r\n"B
      | 424 -> BS"HTTP/1.1 424 Failed Dependency\r\n"B
      | 425 -> BS"HTTP/1.1 425 Unordered Collection\r\n"B
      | 426 -> BS"HTTP/1.1 426 Upgrade Required\r\n"B
      | 428 -> BS"HTTP/1.1 428 Precondition Required\r\n"B
      | 429 -> BS"HTTP/1.1 429 Too Many Requests\r\n"B
      | 431 -> BS"HTTP/1.1 431 Request Header Fields Too Large\r\n"B
      | 451 -> BS"HTTP/1.1 451 Unavailable For Legal Reasons\r\n"B
      | 500 -> BS"HTTP/1.1 500 Internal Server Error\r\n"B
      | 501 -> BS"HTTP/1.1 501 Not Implemented\r\n"B
      | 502 -> BS"HTTP/1.1 502 Bad Gateway\r\n"B
      | 503 -> BS"HTTP/1.1 503 Service Unavailable\r\n"B
      | 504 -> BS"HTTP/1.1 504 Gateway Timeout\r\n"B
      | 505 -> BS"HTTP/1.1 505 HTTP Version Not Supported\r\n"B
      | 506 -> BS"HTTP/1.1 506 Variant Also Negotiates\r\n"B
      | 507 -> BS"HTTP/1.1 507 Insufficient Storage\r\n"B
      | 508 -> BS"HTTP/1.1 508 Loop Detected\r\n"B
      | 509 -> BS"HTTP/1.1 509 Bandwidth Limit Exceeded\r\n"B
      | 510 -> BS"HTTP/1.1 510 Not Extended\r\n"B
      | 511 -> BS"HTTP/1.1 511 Network Authentication Required\r\n"B
      | _ -> BS"HTTP/1.1 500 Internal Server Error\r\n"B

    [<CompiledName("HeadersToBytes")>]
    let headersToBytes (env: #Environment) = 
        String.Join(
            "\r\n",
            [|  yield sprintf "HTTP/1.1 %i %A" env.ResponseStatusCode (Enum.ToObject(typeof<HttpStatusCode>, env.ResponseStatusCode))
                for KeyValue(header, values) in env.ResponseHeaders do
                    // TODO: Fix handling of certain headers where this approach is invalid, e.g. Set-Cookie
                    yield sprintf "%s: %s" header <| String.Join(",", values)
                // Add the body separator.
                yield "\r\n"
            |])
        |> Encoding.ASCII.GetBytes

type ParserDelegate(app, send) as p =
    [<DefaultValue>] val mutable private httpMethod : string
    [<DefaultValue>] val mutable private headerName : string
    [<DefaultValue>] val mutable private headerValue : string
    [<DefaultValue>] val mutable private env : ServerEnvironment
    [<DefaultValue>] val mutable private finished : bool

    let app env keepAlive = async {
        do! app (env :> IDictionary<_,_>)
        send keepAlive <| Response.headersToBytes env
        send keepAlive <| (env.ResponseBody :?> IO.MemoryStream).ToArray()
        send keepAlive [||]
    }

    let commitHeader() = 
        p.env.RequestHeaders.[p.headerName] <- [|p.headerValue|]
        p.headerName <- Unchecked.defaultof<_>
        p.headerValue <- Unchecked.defaultof<_>

    let toHttpStatusCode (i:int) = Enum.ToObject(typeof<HttpStatusCode>, i)

    let onHeadersEnd = Event<Environment>()
    let onDataReceived = Event<ArraySegment<byte>>()
    let onMessageEnd = Event<Environment>()

    [<CLIEvent>]
    member p.OnHeadersEnd = onHeadersEnd.Publish
    [<CLIEvent>]
    member p.OnDataReceived = onDataReceived.Publish
    [<CLIEvent>]
    member p.OnMessageEnd = onMessageEnd.Publish

    interface IHttpParserHandler with
        member this.OnMessageBegin(parser: HttpParser) =
            this.finished <- false
            this.httpMethod <- Unchecked.defaultof<_>
            this.headerName <- Unchecked.defaultof<_>
            this.headerValue <- Unchecked.defaultof<_>
            this.env <- new ServerEnvironment()

        member this.OnMethod( parser, m) = 
            this.httpMethod <- m
            this.env.Add(Owin.Constants.requestMethod, m)

        member this.OnRequestUri(_, requestUri) = 
            let uri = Uri(requestUri, UriKind.RelativeOrAbsolute)
            this.env.Add("fracture.RequestUri", uri)

            // TODO: Fix this so that the path can be determined correctly.
            this.env.Add(Owin.Constants.requestPathBase, "")

            if uri.IsAbsoluteUri then
                this.env.Add(Owin.Constants.requestPath, uri.AbsolutePath)
                this.env.Add(Owin.Constants.requestScheme, uri.Scheme)
                this.env.RequestHeaders.Add("Host", [|uri.Host|])
            else
                this.env.Add(Owin.Constants.requestPath, requestUri)

        member this.OnFragment(_, fragment) = 
            this.env.Add("fracture.RequestFragment", fragment)

        member this.OnQueryString(_, queryString) = 
            this.env.Add(Owin.Constants.requestQueryString, queryString)

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

            this.env.Add("fracture.HttpVersion", Version(parser.MajorVersion, parser.MinorVersion))

            // NOTE: This isn't technically correct as a body with these methods is undefined, not unallowed.
            if this.httpMethod = "GET" || this.httpMethod = "HEAD" then
                this.finished <- true

            onHeadersEnd.Trigger(this.env)

        member this.OnBody(_, data) =
            // XXX can we defer this check to the parser?
            if data.Count > 0 && not this.finished then
                this.env.RequestBody.Write(data.Array, data.Offset, data.Count)
                onDataReceived.Trigger(data)

        member this.OnMessageEnd(parser) =
            onMessageEnd.Trigger(this.env)

            // Execute the application.
            let keepAlive = parser.ShouldKeepAlive
            Async.StartImmediate <| app this.env keepAlive

            // Reset the parser in the event of keep alives.
            this.finished <- false
            this.httpMethod <- Unchecked.defaultof<_>
            this.headerName <- Unchecked.defaultof<_>
            this.headerValue <- Unchecked.defaultof<_>
            this.env <- new ServerEnvironment()
