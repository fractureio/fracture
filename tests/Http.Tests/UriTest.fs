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
module Fracture.Http.Tests.UriTest

open System
open FParsec.Error
open FParsec.Primitives
open FParsec.CharParsers
open Fracture.Http.Primitives
open Fracture.Http.CharParsers
open Fracture.Http.Uri
open NUnit.Framework
open FsUnit

let run p input =
  match run p input with
  | Success(actual,_,_) -> actual
  | Failure(error,_,_) -> Unchecked.defaultof<_>

let ``user info cases`` = [|
  [| box ""; box [] |]
  [| box "ryan"; box [for c in "ryan" -> c] |]
  [| box "ryan:passwd"; box [for c in "ryan:passwd" -> c] |]
  [| box "ryan&passwd"; box [for c in "ryan&passwd" -> c] |]
  [| box "ryan;passwd"; box [for c in "ryan;passwd" -> c] |]
  [| box "ryan=passwd"; box [for c in "ryan=passwd" -> c] |]
  [| box "ryan+"; box [for c in "ryan+" -> c] |]
  [| box "ryan$"; box [for c in "ryan$" -> c] |]
  [| box "ryan%20riley"; box [for c in "ryan riley" -> c] |]
  [| box "ryan%2friley"; box [for c in "ryan/riley" -> c] |]
  [| box "ryan%2Friley"; box [for c in "ryan/riley" -> c] |]
|]
[<Test>]
[<TestCaseSource("user info cases")>]
let ``test userInfo should parse the user info portion of a uri``(input, expected) =
  run userInfo input |> should equal expected

let ``scheme cases`` = [|
  [| box ""; box Unchecked.defaultof<_> |]
  [| box "http"; box (Scheme "http") |]
  [| box "http://boo"; box (Scheme "http") |]
  [| box "https"; box (Scheme "https") |]
  [| box "https://boo"; box (Scheme "https") |]
  [| box "urn"; box (Scheme "urn") |]
  [| box "urn:"; box (Scheme "urn") |]
  [| box "ftp"; box (Scheme "ftp") |]
  [| box "ftp:"; box (Scheme "ftp") |]
  [| box "mailto"; box (Scheme "mailto") |]
  [| box "mailto:ryan@owin.org"; box (Scheme "mailto") |]
  [| box "owin.org"; box (Scheme "owin.org") |]
  [| box "owin:org"; box (Scheme "owin") |]
  [| box "owin.org:"; box (Scheme "owin.org") |]
|]
[<Test>]
[<TestCaseSource("scheme cases")>]
let ``test scheme should parse a Scheme``(input, expected: UriPart) =
  run scheme input |> should equal expected

let ``hostport cases`` = [|
  [| box ""; box Unchecked.defaultof<_> |]
  [| box ":80"; box Unchecked.defaultof<_> |]
  [| box "owin.org"; box [Host "owin.org"] |]
  [| box "owin.org/"; box [Host "owin.org"] |]
  [| box "owin.org/blah"; box [Host "owin.org"] |]
  [| box "owin.org:"; box [Host "owin.org"; Port ""] |]
  [| box "owin.org:80"; box [Host "owin.org"; Port "80"] |]
  [| box "owin.org:80/"; box [Host "owin.org"; Port "80"] |]
  [| box "owin.org:80/blah"; box [Host "owin.org"; Port "80"] |]
  [| box "owin.org:8080"; box [Host "owin.org"; Port "8080"] |]
  [| box "owin.org:8080/"; box [Host "owin.org"; Port "8080"] |]
  [| box "owin.org:8080/blah"; box [Host "owin.org"; Port "8080"] |]
  [| box "192.168.0.1"; box Unchecked.defaultof<_> |]
  [| box "192.168.0.1/"; box Unchecked.defaultof<_> |]
  [| box "192.168.0.1:80"; box Unchecked.defaultof<_> |]
  [| box "192.168.0.1:80/"; box Unchecked.defaultof<_> |]
|]
[<Test>]
[<TestCaseSource("hostport cases")>]
let ``test hostport should parse a list of Host and optional Port``(input, expected: UriPart list) =
  run hostport input |> should equal expected

let ``server cases`` = [|
//  [| box "wizardsofsmart.net"; box [Host "wizardsofsmart.net"] |]
//  [| box "wizardsofsmart.net/"; box [Host "wizardsofsmart.net"] |]
//  [| box "wizardsofsmart.net:80"; box [Host "wizardsofsmart.net"; Port "80"] |]
//  [| box "wizardsofsmart.net:80/"; box [Host "wizardsofsmart.net"; Port "80"] |]
  [| box "ryan@wizardsofsmart.net"; box [UserInfo "ryan"; Host "wizardsofsmart.net"] |]
  [| box "ryan@wizardsofsmart.net/"; box [UserInfo "ryan"; Host "wizardsofsmart.net"] |] 
  [| box "ryan@wizardsofsmart.net:80"; box [UserInfo "ryan"; Host "wizardsofsmart.net"; Port "80"] |]
  [| box "ryan@wizardsofsmart.net:80/"; box [UserInfo "ryan"; Host "wizardsofsmart.net"; Port "80"] |]
  [| box "ryan:passwd@wizardsofsmart.net"; box [UserInfo "ryan:passwd"; Host "wizardsofsmart.net"] |] 
  [| box "ryan:passwd@wizardsofsmart.net/"; box [UserInfo "ryan:passwd"; Host "wizardsofsmart.net"] |] 
  [| box "ryan:passwd@wizardsofsmart.net:80"; box [UserInfo "ryan:passwd"; Host "wizardsofsmart.net"; Port "80"] |]
  [| box "ryan:passwd@wizardsofsmart.net:80/"; box [UserInfo "ryan:passwd"; Host "wizardsofsmart.net"; Port "80"] |]
|]
[<Test>]
[<TestCaseSource("server cases")>]
let ``test server should parse an optional UserInfo, Host and optional Port``(input, expected: UriPart list) =
  run server input |> should equal expected

let ``uri cases`` = [|
  [| box "#frag"; box (FragmentRef(Fragment "frag")) |]
  [| box "/"; box (RelativeUri [Path "/"; QueryString null; Fragment null]) |]
  [| box "/?foo=bar"; box (RelativeUri [Path "/"; QueryString "foo=bar"; Fragment null]) |]
  [| box "/?foo=1"; box (RelativeUri [Path "/"; QueryString "foo=1"; Fragment null]) |]
  [| box "/#frag"; box (RelativeUri [Path "/"; QueryString null; Fragment "frag"]) |]
  [| box "http://owin.org"; box (AbsoluteUri [Scheme "http"; Host "owin.org"; Path "/"; QueryString null; Fragment null]) |]
  [| box "http://owin.org/"; box (AbsoluteUri [Scheme "http"; Host "owin.org"; Path "/"; QueryString null; Fragment null]) |]
  [| box "http://owin.org/?foo=bar"; box (AbsoluteUri [Scheme "http"; Host "owin.org"; Path "/"; QueryString "foo=bar"; Fragment null]) |]
  [| box "http://owin.org/?foo=1"; box (AbsoluteUri [Scheme "http"; Host "owin.org"; Path "/"; QueryString "foo=1"; Fragment null]) |]
  [| box "http://owin.org/#frag"; box (AbsoluteUri [Scheme "http"; Host "owin.org"; Path "/"; QueryString null; Fragment "frag"]) |]
  [| box "http://192.168.0.1"; box (AbsoluteUri [Scheme "http"; Host "192.168.0.1"; Path "/"; QueryString null; Fragment null]) |]
  [| box "http://192.168.0.1/"; box (AbsoluteUri [Scheme "http"; Host "192.168.0.1"; Path "/"; QueryString null; Fragment null]) |]
  [| box "http://192.168.0.1/?foo=bar"; box (AbsoluteUri [Scheme "http"; Host "192.168.0.1"; Path "/"; QueryString "foo=bar"; Fragment null]) |]
  [| box "http://192.168.0.1/?foo=1"; box (AbsoluteUri [Scheme "http"; Host "192.168.0.1"; Path "/"; QueryString "foo=1"; Fragment null]) |]
  [| box "http://192.168.0.1/#frag"; box (AbsoluteUri [Scheme "http"; Host "192.168.0.1"; Path "/"; QueryString null; Fragment "frag"]) |]
|]
[<Test>]
[<TestCaseSource("uri cases")>]
let ``test uriReference should parse absolute, relative and fragment references``(input, expected) =
  run uriReference input |> should equal expected 
