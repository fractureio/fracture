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
module Fracture.Http.Tests.CharParsersTest

open System
open FParsec.CharParsers
open Fracture.Http.Hex
open Fracture.Http.Primitives
open Fracture.Http.CharParsers
open NUnit.Framework
open FsUnit

let run p input =
  match run p input with
  | Success(actual,_,_) -> actual
  | Failure(error,_,_) -> failwith error

[<TestCase(" ", Result=' ')>]
[<TestCase("0", ExpectedException=typeof<Exception>)>]
let ``test space parser should parse a ' '``(input) = run space input

[<TestCase("\"", Result='"')>]
[<TestCase(" ", ExpectedException=typeof<Exception>)>]
let ``test dquote parser should parse a '"'``(input) = run dquote input
 
[<TestCase("#", Result='#')>]
[<TestCase(" ", ExpectedException=typeof<Exception>)>]
let ``test hash parser should parse a '#'``(input) = run hash input

[<TestCase("%", Result='%')>]
[<TestCase(" ", ExpectedException=typeof<Exception>)>]
let ``test percent parser should parse a '%'``(input) = run percent input

[<TestCase("+", Result='+')>]
[<TestCase(" ", ExpectedException=typeof<Exception>)>]
let ``test plus parser should parse a '+'``(input) = run plus input
 
[<TestCase("-", Result='-')>]
[<TestCase(" ", ExpectedException=typeof<Exception>)>]
let ``test hyphen parser should parse a '-'``(input) = run hyphen input
 
[<TestCase(".", Result='.')>]
[<TestCase(" ", ExpectedException=typeof<Exception>)>]
let ``test dot parser should parse a '.'``(input) = run dot input

[<TestCase(":", Result=':')>]
[<TestCase(" ", ExpectedException=typeof<Exception>)>]
let ``test colon parser should parse a ':'``(input) = run colon input

[<TestCase(";", Result=';')>]
[<TestCase(" ", ExpectedException=typeof<Exception>)>]
let ``test semicolon parser should parse a ';'``(input) = run semicolon input

[<TestCase("/", Result='/')>]
[<TestCase(" ", ExpectedException=typeof<Exception>)>]
let ``test slash parser should parse a '/'``(input) = run slash input
 
[<TestCase("?", Result='?')>]
[<TestCase(" ", ExpectedException=typeof<Exception>)>]
let ``test qmark parser should parse a '?'``(input) = run qmark input

[<TestCase("@", Result='@')>]
[<TestCase(" ", ExpectedException=typeof<Exception>)>]
let ``test qmark parser should parse an at-sign``(input) = run at input

[<TestCase("%20", Result=' ')>]
[<TestCase("%2F", Result='/')>]
[<TestCase("%2f", Result='/')>]
let ``test escaped parser returns the byte representing the full hexadecimal character``(input) = run escaped input