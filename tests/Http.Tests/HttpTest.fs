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
module Fracture.Http.Tests.HttpTest

open System
open FParsec.Error
open FParsec.Primitives
open FParsec.CharParsers
open Fracture.Http.Primitives
open Fracture.Http.CharParsers
open Fracture.Http.Uri
open Fracture.Http.Http
open NUnit.Framework
open FsUnit

type TestHandler() =
  let mutable onMessageBeginWasCalled = false
  let mutable startLine = Unchecked.defaultof<HttpRequestLine>
  let mutable headers : HttpHeader list = []
  let mutable onHeadersEndWasCalled = false
  let mutable body = Unchecked.defaultof<HttpMessageBody>
  let mutable onMessageEndWasCalled = false
  let mutable error = Unchecked.defaultof<ParserError>

  member this.OnHttpMessageBeginWasCalled = onMessageBeginWasCalled
  member this.StartLine = startLine
  member this.Headers = headers
  member this.OnHeadersEndWasCalled = onHeadersEndWasCalled
  member this.Body = body
  member this.OnHttpMessageEndWasCalled = onMessageEndWasCalled
  member this.Error = error

  interface IHttpMessageParserHandler with
    member this.OnHttpMessageBegin() = onMessageBeginWasCalled <- true
    member this.OnStartLine(v) = startLine <- v
    member this.OnHeader(v) = headers <- v::headers
    member this.OnHeadersEnd() = onHeadersEndWasCalled <- true
    member this.OnBody(v) = body <- v
    member this.OnHttpMessageEnd() = onMessageEndWasCalled <- true
    member this.OnError(e) = error <- e

let run p input =
  match run p input with
  | Success(actual,_,_) -> actual
  | Failure(error,_,_) -> failwith error

let runWithHandler p handler input =
  match runParserOnString p handler "test" input with
  | Success(actual,_,_) -> actual
  | Failure(error,_,_) -> failwith error

[<TestCase(" ", Result=' ')>]
[<TestCase("\t", Result=' ')>]
[<TestCase("\n ", Result=' ')>]
[<TestCase("\r ", Result=' ')>]
[<TestCase("\r\n ", Result=' ')>]
[<TestCase("\n  ", Result=' ')>]
[<TestCase("\r  ", Result=' ')>]
[<TestCase("\r\n  ", Result=' ')>]
[<TestCase("\n\t", Result=' ')>]
[<TestCase("\r\t", Result=' ')>]
[<TestCase("\r\n\t", Result=' ')>]
[<TestCase("\n\t\t", Result=' ')>]
[<TestCase("\r\t\t", Result=' ')>]
[<TestCase("\r\n\t\t", Result=' ')>]
[<TestCase("\n \t", Result=' ')>]
[<TestCase("\r \t", Result=' ')>]
[<TestCase("\r\n \t", Result=' ')>]
[<TestCase("\n\t ", Result=' ')>]
[<TestCase("\r\t ", Result=' ')>]
[<TestCase("\r\n\t ", Result=' ')>]
[<TestCase("", ExpectedException=typeof<Exception>)>]
[<TestCase("\n", ExpectedException=typeof<Exception>)>]
[<TestCase("\r", ExpectedException=typeof<Exception>)>]
[<TestCase("\r\n", ExpectedException=typeof<Exception>)>]
let ``test lws should parse one or more white spaces, including an optional line fold``(input) = run lws input

// Test HTTP Request Method parser
let testHttpMethods = [|
  [| box "OPTIONS"; box OPTIONS |]
  [| box "GET"; box GET |]
  [| box "HEAD"; box HEAD |]
  [| box "POST"; box POST |]
  [| box "PUT"; box PUT |]
  [| box "DELETE"; box DELETE |]
  [| box "TRACE"; box TRACE |]
  [| box "CONNECT"; box CONNECT |]
  [| box "options"; box (ExtensionMethod "options") |]
  [| box "get"; box (ExtensionMethod "get") |]
  [| box "head"; box (ExtensionMethod "head") |]
  [| box "post"; box (ExtensionMethod "post") |]
  [| box "put"; box (ExtensionMethod "put") |]
  [| box "delete"; box (ExtensionMethod "delete") |]
  [| box "trace"; box (ExtensionMethod "trace") |]
  [| box "connect"; box (ExtensionMethod "connect") |]
  [| box "PATCH"; box (ExtensionMethod "PATCH") |]
  [| box "David"; box (ExtensionMethod "David") |]
  [| box "frank"; box (ExtensionMethod "frank") |]
|]
[<TestCaseSource("testHttpMethods")>]
let ``test httpMethod should parse valid HTTP methods``(input, expected) =
  run httpMethod input |> should equal expected

[<TestCase("", ExpectedException=typeof<Exception>)>]
[<TestCase("(", ExpectedException=typeof<Exception>)>]
[<TestCase(")", ExpectedException=typeof<Exception>)>]
[<TestCase("<", ExpectedException=typeof<Exception>)>]
[<TestCase(">", ExpectedException=typeof<Exception>)>]
[<TestCase("@", ExpectedException=typeof<Exception>)>]
[<TestCase(",", ExpectedException=typeof<Exception>)>]
[<TestCase(";", ExpectedException=typeof<Exception>)>]
[<TestCase(":", ExpectedException=typeof<Exception>)>]
[<TestCase("\\", ExpectedException=typeof<Exception>)>]
[<TestCase("\"", ExpectedException=typeof<Exception>)>]
[<TestCase("/", ExpectedException=typeof<Exception>)>]
[<TestCase("[", ExpectedException=typeof<Exception>)>]
[<TestCase("]", ExpectedException=typeof<Exception>)>]
[<TestCase("?", ExpectedException=typeof<Exception>)>]
[<TestCase("=", ExpectedException=typeof<Exception>)>]
[<TestCase("{", ExpectedException=typeof<Exception>)>]
[<TestCase("}", ExpectedException=typeof<Exception>)>]
[<TestCase(" ", ExpectedException=typeof<Exception>)>]
[<TestCase("\t", ExpectedException=typeof<Exception>)>]
let ``test httpMethod should not parse invalid HTTP methods``(input) = run httpMethod input

// Test HTTP Request URI parser
let testRequestUris = [|
  [| box "*"; box (function AnyUri -> true | _ -> false) |]
  [| box "/path/to"; box (function RelativeUri _ -> true | _ -> false) |]
  [| box "/path/to/"; box (function RelativeUri _ -> true | _ -> false) |]
  [| box "/path/to#frag"; box (function RelativeUri _ -> true | _ -> false) |]
  [| box "/path/to/#frag"; box (function RelativeUri _ -> true | _ -> false) |]
  [| box "/path/to?place=there"; box (function RelativeUri _ -> true | _ -> false) |]
  [| box "/path/to/?place=there"; box (function RelativeUri _ -> true | _ -> false) |]
  [| box "/path/to?place=there#frag"; box (function RelativeUri _ -> true | _ -> false) |]
  [| box "/path/to/?place=there#frag"; box (function RelativeUri _ -> true | _ -> false) |]
//  [| box "wizardsofsmart.net"; box (function UriAuthority _ -> true | _ -> false) |]
//  [| box "wizardsofsmart.net/"; box (function UriAuthority _ -> true | _ -> false) |]
//  [| box "wizardsofsmart.net:80"; box (function UriAuthority _ -> true | _ -> false) |]
//  [| box "wizardsofsmart.net:80/"; box (function UriAuthority _ -> true | _ -> false) |]
//  [| box "ryan@wizardsofsmart.net"; box (function UriAuthority _ -> true | _ -> false) |]
//  [| box "ryan@wizardsofsmart.net/"; box (function UriAuthority _ -> true | _ -> false) |]
//  [| box "ryan@wizardsofsmart.net:80"; box (function UriAuthority _ -> true | _ -> false) |]
//  [| box "ryan@wizardsofsmart.net:80/"; box (function UriAuthority _ -> true | _ -> false) |]
//  [| box "ryan:passwd@wizardsofsmart.net"; box (function UriAuthority _ -> true | _ -> false) |]
//  [| box "ryan:passwd@wizardsofsmart.net/"; box (function UriAuthority _ -> true | _ -> false) |]
//  [| box "ryan:passwd@wizardsofsmart.net:80"; box (function UriAuthority _ -> true | _ -> false) |]
//  [| box "ryan:passwd@wizardsofsmart.net:80/"; box (function UriAuthority _ -> true | _ -> false) |]
  [| box "http://wizardsofsmart.net"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://wizardsofsmart.net/"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://wizardsofsmart.net#frag"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://wizardsofsmart.net/#frag"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://wizardsofsmart.net:80"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://wizardsofsmart.net:80/"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://wizardsofsmart.net:80#frag"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://wizardsofsmart.net:80/#frag"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://wizardsofsmart.net/path/to"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://wizardsofsmart.net/path/to/"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://wizardsofsmart.net/path/to#frag"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://wizardsofsmart.net/path/to/#frag"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://wizardsofsmart.net/path/to?place=there"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://wizardsofsmart.net/path/to/?place=there"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://wizardsofsmart.net/path/to?place=there#frag"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://wizardsofsmart.net/path/to/?place=there#frag"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://wizardsofsmart.net/path/to?place=there/#frag"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://wizardsofsmart.net/path/to/?place=there/#frag"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://192.168.0.1"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://192.168.0.1/"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://192.168.0.1#frag"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://192.168.0.1/#frag"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://192.168.0.1:80"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://192.168.0.1:80/"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://192.168.0.1:80#frag"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://192.168.0.1:80/#frag"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://192.168.0.1/path/to"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://192.168.0.1/path/to/"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://192.168.0.1/path/to#frag"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://192.168.0.1/path/to/#frag"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://192.168.0.1/path/to?place=there"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://192.168.0.1/path/to/?place=there"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://192.168.0.1/path/to?place=there#frag"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://192.168.0.1/path/to/?place=there#frag"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://192.168.0.1/path/to?place=there/#frag"; box (function AbsoluteUri _ -> true | _ -> false) |]
  [| box "http://192.168.0.1/path/to/?place=there/#frag"; box (function AbsoluteUri _ -> true | _ -> false) |]
|]
[<Test>]
[<TestCaseSource("testRequestUris")>]
let ``test requestUri should parse valid absolute and relative uris, uri authority, and the wildcard``(input, isUri:UriKind -> bool) =
  let actual = run httpRequestUri input
  Assert.That(actual |> isUri)

[<TestCase("#frag", ExpectedException=typeof<Exception>)>]
let ``test requestUri does not accept uri fragments``(input) = run httpRequestUri input

// Test HTTP Version parser
[<TestCase("HTTP/1.0", 1, 0)>]
[<TestCase("HTTP/1.1", 1, 1)>]
[<TestCase("HTTP/2.4", 2, 4)>]
[<TestCase("HTTP/2.13", 2, 13)>]
[<TestCase("HTTP/11.0", 11, 0)>]
[<TestCase("HTTP/11.1", 11, 1)>]
let ``test httpVersion should parse HTTP/major_minor``(input, major, minor) =
  run httpVersion input |> should equal (HttpVersion(major, minor))

// Test HTTP Request Line parser
let testHttpRequestLines = [|
  [| box "GET * HTTP/1.0\r\n"; box (HttpRequestLine(GET, AnyUri, HttpVersion(1,0))) |]
  [| box "GET * HTTP/1.1\r\n"; box (HttpRequestLine(GET, AnyUri, HttpVersion(1,1))) |]
  [| box "GET / HTTP/1.1\r\n"; box (HttpRequestLine(GET, RelativeUri([Path "/"; QueryString null; Fragment null]), HttpVersion(1,1))) |]
  [| box "POST / HTTP/1.1\r\n"; box (HttpRequestLine(POST, RelativeUri([Path "/"; QueryString null; Fragment null]), HttpVersion(1,1))) |]
  [| box "PATCH / HTTP/1.1\r\n"; box (HttpRequestLine(ExtensionMethod("PATCH"), RelativeUri([Path "/"; QueryString null; Fragment null]), HttpVersion(1,1))) |]
  [| box "GET /orders HTTP/1.1\r\n"; box (HttpRequestLine(GET, RelativeUri([Path "/orders"; QueryString null; Fragment null]), HttpVersion(1,1))) |]
  [| box "GET /orders?id=1 HTTP/1.1\r\n"; box (HttpRequestLine(GET, RelativeUri([Path "/orders"; QueryString "id=1"; Fragment null]), HttpVersion(1,1))) |]
  [| box "GET /orders#last HTTP/1.1\r\n"; box (HttpRequestLine(GET, RelativeUri([Path "/orders"; QueryString null; Fragment "last"]), HttpVersion(1,1))) |]
  // Not strictly correct but common from many clients.
  [| box "GET * HTTP/1.1\r"; box (HttpRequestLine(GET, AnyUri, HttpVersion(1,1))) |]
  [| box "POST / HTTP/1.1\r"; box (HttpRequestLine(POST, RelativeUri([Path "/"; QueryString null; Fragment null]), HttpVersion(1,1))) |]
  [| box "PATCH / HTTP/1.1\r"; box (HttpRequestLine(ExtensionMethod("PATCH"), RelativeUri([Path "/"; QueryString null; Fragment null]), HttpVersion(1,1))) |]
  [| box "GET * HTTP/1.1\n"; box (HttpRequestLine(GET, AnyUri, HttpVersion(1,1))) |]
  [| box "POST / HTTP/1.1\n"; box (HttpRequestLine(POST, RelativeUri([Path "/"; QueryString null; Fragment null]), HttpVersion(1,1))) |]
  [| box "PATCH / HTTP/1.1\n"; box (HttpRequestLine(ExtensionMethod("PATCH"), RelativeUri([Path "/"; QueryString null; Fragment null]), HttpVersion(1,1))) |]
|]
[<Test>]
[<TestCaseSource("testHttpRequestLines")>]
let ``test httpRequestLine should parse an HTTP request line, including the newline``(input, expected:HttpRequestLine) =
  let handler = TestHandler() :> IHttpMessageParserHandler
  runWithHandler httpRequestLine handler input |> should equal expected

[<TestCase("", ExpectedException=typeof<Exception>)>]
[<TestCase("GET", ExpectedException=typeof<Exception>)>]
[<TestCase("GET *", ExpectedException=typeof<Exception>)>]
[<TestCase("GET HTTP/1.0", ExpectedException=typeof<Exception>)>]
[<TestCase("GET HTTP/1.1", ExpectedException=typeof<Exception>)>]
[<TestCase("GET * HTTP/1.0", ExpectedException=typeof<Exception>)>]
[<TestCase("GET * HTTP/1.1", ExpectedException=typeof<Exception>)>]
[<TestCase("\r\n", ExpectedException=typeof<Exception>)>]
[<TestCase("GET\r\n", ExpectedException=typeof<Exception>)>]
[<TestCase("GET *\r\n", ExpectedException=typeof<Exception>)>]
[<TestCase("GET HTTP/1.0\r\n", ExpectedException=typeof<Exception>)>]
[<TestCase("GET HTTP/1.1\r\n", ExpectedException=typeof<Exception>)>]
let ``test httpRequestLine should not parse invalid HTTP request lines``(input) =
  let handler = TestHandler() :> IHttpMessageParserHandler
  runWithHandler httpStatusLine handler input |> should equal expected

let testHttpStatusLines = [|
  [| box "100 Continue\n"; box (HttpStatusLine(100, "Continue")) |]
  [| box "200 OK\r"; box (HttpStatusLine(200, "OK")) |]
  [| box "302 Found\r\n"; box (HttpStatusLine(302, "Found")) |]
  [| box "404 Not Found\r"; box (HttpStatusLine(404, "Not Found")) |]
  [| box "405 Method Not Allowed\n"; box (HttpStatusLine(405, "Method Not Allowed")) |]
  [| box "406 Not Acceptable\r\n"; box (HttpStatusLine(406, "Not Acceptable")) |]
|]
[<Test>]
[<TestCaseSource("testHttpStatusLines")>]
let ``test httpStatusLine should parse an HTTP response status line, including the newline``(input, expected:HttpStatusLine) =
  let handler = TestHandler() :> IHttpMessageParserHandler
  runWithHandler httpStatusLine handler input |> should equal expected

// Test HTTP Request Header parser
let testHttpHeader = [|
  [| box "Accept: application/json\r"; box (HttpHeader("Accept", "application/json")) |]
  [| box "Accept: application/json\n"; box (HttpHeader("Accept", "application/json")) |]
  [| box "Accept: application/json\r\n"; box (HttpHeader("Accept", "application/json")) |]
|]
[<Test>]
[<TestCaseSource("testHttpHeader")>]
let ``test httpHeader should parse a valid HTTP header, including the newline``(input, expected:HttpHeader) =
  let handler = TestHandler() :> IHttpMessageParserHandler
  runWithHandler httpHeader handler input |> should equal expected

let date = System.DateTime.UtcNow.ToLongDateString()
let testHttpHeaders = [|
  [| box ""; box ([]:HttpHeader list) |]
  [| box "Accept: application/json\r"; box [HttpHeader("Accept", "application/json")] |]
  [| box "Accept: application/json\n"; box [HttpHeader("Accept", "application/json")] |]
  [| box "Accept: application/json\r\n"; box [HttpHeader("Accept", "application/json")] |]
  [| box ("Date: " + date + "\rAccept: application/json\r"); box [HttpHeader("Date", date); HttpHeader("Accept", "application/json")] |]
  [| box ("Date: " + date + "\nAccept: application/json\n"); box [HttpHeader("Date", date); HttpHeader("Accept", "application/json")] |]
  [| box ("Date: " + date + "\r\nAccept: application/json\r\n"); box [HttpHeader("Date", date); HttpHeader("Accept", "application/json")] |]
|]
[<Test>]
[<TestCaseSource("testHttpHeaders")>]
let ``test httpHeaders should parse a set of valid HTTP headers, including the newline``(input, expected:HttpHeader list) =
  let handler = TestHandler() :> IHttpMessageParserHandler
  runWithHandler (many httpHeader) handler input |> should equal expected

// Test HTTP Request Message parser
let testHttpRequests = [|
  [|box "GET * HTTP/1.1\r\n\r\n"
    box (HttpRequestMessage(HttpRequestLine(GET, AnyUri, HttpVersion(1,1)), [], EmptyBody)) |]
  [|box "GET * HTTP/1.1\r\nAccept: application/json\r\n\r\n"
    box (HttpRequestMessage(HttpRequestLine(GET, AnyUri, HttpVersion(1,1)), [HttpHeader("Accept", "application/json")], EmptyBody)) |]
  [|box "GET http://localhost/ HTTP/1.1\r\n\r\n"
    box (HttpRequestMessage(HttpRequestLine(GET, AbsoluteUri [Scheme "http"; Fracture.Http.Uri.Host "localhost"; Path "/"; QueryString null; Fragment null], HttpVersion(1,1)), [], EmptyBody)) |]
  [|box "GET http://localhost/ HTTP/1.1\r\nAccept: application/json\r\n\r\n"
    box (HttpRequestMessage(HttpRequestLine(GET, AbsoluteUri [Scheme "http"; Fracture.Http.Uri.Host "localhost"; Path "/"; QueryString null; Fragment null], HttpVersion(1,1)), [HttpHeader("Accept", "application/json")], EmptyBody)) |]
  [|box "GET / HTTP/1.1\r\nHost: http://localhost\r\n\r\n"
    box (HttpRequestMessage(HttpRequestLine(GET, RelativeUri [Path "/"; QueryString null; Fragment null], HttpVersion(1,1)), [HttpHeader("Host", "http://localhost")], EmptyBody)) |]
  [|box "GET / HTTP/1.1\r\nHost: http://localhost\nAccept: application/json\r\n\r\n"
    box (HttpRequestMessage(HttpRequestLine(GET, RelativeUri [Path "/"; QueryString null; Fragment null], HttpVersion(1,1)), [HttpHeader("Host", "http://localhost"); HttpHeader("Accept", "application/json")], EmptyBody)) |]
  [|box "GET http://192.168.0.1/ HTTP/1.1\r\n\r\n"
    box (HttpRequestMessage(HttpRequestLine(GET, AbsoluteUri [Scheme "http"; Fracture.Http.Uri.Host "192.168.0.1"; Path "/"; QueryString null; Fragment null], HttpVersion(1,1)), [], EmptyBody)) |]
  [|box "GET http://192.168.0.1/ HTTP/1.1\r\nAccept: application/json\r\n\r\n"
    box (HttpRequestMessage(HttpRequestLine(GET, AbsoluteUri [Scheme "http"; Fracture.Http.Uri.Host "192.168.0.1"; Path "/"; QueryString null; Fragment null], HttpVersion(1,1)), [HttpHeader("Accept", "application/json")], EmptyBody)) |]
  [|box "GET / HTTP/1.1\r\nHost: http://192.168.0.1\r\n\r\n"
    box (HttpRequestMessage(HttpRequestLine(GET, RelativeUri [Path "/"; QueryString null; Fragment null], HttpVersion(1,1)), [HttpHeader("Host", "http://192.168.0.1")], EmptyBody)) |]
  [|box "GET / HTTP/1.1\r\nHost: http://192.168.0.1\nAccept: application/json\r\n\r\n"
    box (HttpRequestMessage(HttpRequestLine(GET, RelativeUri [Path "/"; QueryString null; Fragment null], HttpVersion(1,1)), [HttpHeader("Host", "http://192.168.0.1"); HttpHeader("Accept", "application/json")], EmptyBody)) |]
|]
[<Test>]
[<TestCaseSource("testHttpRequests")>]
let ``test httpRequestMessage should parse valid HTTP requests``(input, expected) =
  let handler = TestHandler() :> IHttpMessageParserHandler
  runWithHandler httpRequestMessage handler input |> should equal expected
