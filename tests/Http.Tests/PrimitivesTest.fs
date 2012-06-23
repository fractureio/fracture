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
module Fracture.Http.Tests.PrimitivesTest

open System
open FParsec.CharParsers
open Fracture.Http.Hex
open Fracture.Http.Primitives
open NUnit.Framework
open FsUnit

let run p input =
  match run p input with
  | Success(actual,_,_) -> actual
  | Failure(error,_,_) -> failwith error

[<TestCase('\n',"\n", Result='\n')>]
[<TestCase('\r',"\r", Result='\r')>]
let ``test pchar '\r' and '\n' should return '\n'``(charToParse, input) =
  let p = pchar charToParse
  run p input

[<TestCase('a','b',"ab", Result="ab")>]
[<TestCase('/','/',"//", Result="//")>]
[<TestCase(' ',' ',"  ", Result="  ")>]
let ``test listify2 should run two parsers and put the results in a list``(x, y, input) =
  let p = listify2 (pchar x) (pchar y)
  run p input |> String.ofCharList