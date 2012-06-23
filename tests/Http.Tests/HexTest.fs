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
module Fracture.Http.Tests.HexTest

open Fracture.Http.Hex
open NUnit.Framework
open FsUnit

[<TestCase(0, '0')>]
[<TestCase(1, '1')>]
[<TestCase(2, '2')>]
[<TestCase(3, '3')>]
[<TestCase(4, '4')>]
[<TestCase(5, '5')>]
[<TestCase(6, '6')>]
[<TestCase(7, '7')>]
[<TestCase(8, '8')>]
[<TestCase(9, '9')>]
[<TestCase(10, 'A')>]
[<TestCase(11, 'B')>]
[<TestCase(12, 'C')>]
[<TestCase(13, 'D')>]
[<TestCase(14, 'E')>]
[<TestCase(15, 'F')>]
let ``test should convert a hex digit to a char``(x, expected:char) =
  toHexDigit x |> should equal expected

[<TestCase('0', 0)>]
[<TestCase('1', 1)>]
[<TestCase('2', 2)>]
[<TestCase('3', 3)>]
[<TestCase('4', 4)>]
[<TestCase('5', 5)>]
[<TestCase('6', 6)>]
[<TestCase('7', 7)>]
[<TestCase('8', 8)>]
[<TestCase('9', 9)>]
[<TestCase('A', 10)>]
[<TestCase('B', 11)>]
[<TestCase('C', 12)>]
[<TestCase('D', 13)>]
[<TestCase('E', 14)>]
[<TestCase('F', 15)>]
[<TestCase('a', 10)>]
[<TestCase('b', 11)>]
[<TestCase('c', 12)>]
[<TestCase('d', 13)>]
[<TestCase('e', 14)>]
[<TestCase('f', 15)>]
let ``test should convert a char to a hex digit``(chr, expected:int) =
  fromHexDigit chr |> should equal expected

[<TestCase("20", 32uy)>]
[<TestCase("2F", 47uy)>]
[<TestCase("2f", 47uy)>]
let ``test should decode a hexadecimal string into an integer``(input, expected:byte) =
  (decode input).[0] |> should equal expected
