module Fracture.Http.Tests.HttpTest

open System
open FParsec.Error
open FParsec.CharParsers
open Fracture.Http.Primitives
open Fracture.Http.CharParsers
open Fracture.Http.Uri
open Fracture.Http.Http
open NUnit.Framework
open FsUnit

let run p input =
  match run p input with
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
|]
[<Test>]
[<TestCaseSource("testRequestUris")>]
let ``test requestUri should parse valid absolute and relative uris, uri authority, and the wildcard``(input, isUri:UriKind -> bool) =
  let actual = run httpRequestUri input
  Assert.That(actual |> isUri)

[<TestCase("#frag", ExpectedException=typeof<Exception>)>]
let ``test requestUri does not accept uri fragments``(input) = run httpRequestUri input

[<TestCase("HTTP/1.0", 1, 0)>]
[<TestCase("HTTP/1.1", 1, 1)>]
[<TestCase("HTTP/2.4", 2, 4)>]
[<TestCase("HTTP/2.13", 2, 13)>]
[<TestCase("HTTP/11.0", 11, 0)>]
[<TestCase("HTTP/11.1", 11, 1)>]
let ``test httpVersion should parse HTTP/major_minor``(input, major, minor) =
  run httpVersion input |> should equal (HttpVersion(major, minor))

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
  let actual = run httpRequestLine input
  actual |> should equal expected

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
let ``test httpRequestLine should not parse invalid HTTP request lines``(input) = run httpRequestLine input
