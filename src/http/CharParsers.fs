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
module Fracture.Http.CharParsers

open System
open FParsec.Primitives
open FParsec.CharParsers
open Primitives

let internal controlChars = String(127uy::[0uy..31uy] |> List.map char |> Array.ofList)
let control<'a> : Parser<char,'a> = anyOf controlChars
let alphanum<'a> : Parser<char, 'a> = asciiLetter <|> digit
let space<'a> : Parser<char, 'a> = pchar ' '
let skipSpace<'a> : Parser<unit, 'a> = skipChar ' '
let dquote<'a> : Parser<char, 'a> = pchar '"'
let hash<'a> : Parser<char, 'a> = pchar '#'
let percent<'a> : Parser<char, 'a> = pchar '%'
let plus<'a> : Parser<char, 'a> = pchar '+'
let hyphen<'a> : Parser<char, 'a> = pchar '-'
let dot<'a> : Parser<char, 'a> = pchar '.'
let colon<'a> : Parser<char, 'a> = pchar ':'
let semicolon<'a> : Parser<char, 'a> = pchar ';'
let slash<'a> : Parser<char, 'a> = pchar '/'
let qmark<'a> : Parser<char, 'a> = pchar '?'
let at<'a> : Parser<char, 'a> = pchar '@'
let escaped<'a> : Parser<char, 'a> = pipe2 (percent >>. hex) hex readHex
