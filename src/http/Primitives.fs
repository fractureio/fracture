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
module Fracture.Http.Primitives

open System
open FParsec.Primitives
open Fracture.Http.Hex

module String =
  let ofCharList (input:char list) = String(Array.ofList input)
  let toCharList (input:String) = input.ToCharArray() |> Array.toList

let private wsChars = [|' ';'\t'|]
type String with
  member x.TrimWhiteSpace() = x.Trim(wsChars)

let inline (<*>) f a = f >>= fun f' -> a >>= fun a' -> preturn (f' a')
let inline ( !!) s = String.ofCharList s
let readHex a b = fromHexDigit a <<< 4 ||| fromHexDigit b |> char
let listify a = a |>> (fun x -> [x])
let listify2 a b = pipe2 a b <| fun a b -> a::[b]
let cons hd tl = hd::tl
let flatten hd tl = hd::tl |> List.concat
