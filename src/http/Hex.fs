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
/// A hexadecimal converter.
/// See http://fssnip.net/25
module Fracture.Http.Hex

open System

[<CompiledName("ToHexDigit")>]
let toHexDigit n =
  if n < 10 then char (n + 0x30) else char (n + 0x37)

[<CompiledName("FromHexDigit")>]
let fromHexDigit c =
  if c >= '0' && c <= '9' then int c - int '0'
  elif c >= 'A' && c <= 'F' then (int c - int 'A') + 10
  elif c >= 'a' && c <= 'f' then (int c - int 'a') + 10
  else raise <| new ArgumentException()
  
[<CompiledName("Encode")>]
let encode (buf:byte array) (prefix:bool) =
  let hex = Array.zeroCreate (buf.Length * 2)
  let mutable n = 0
  for i = 0 to buf.Length - 1 do
    hex.[n] <- toHexDigit ((int buf.[i] &&& 0xF0) >>> 4)
    n <- n + 1
    hex.[n] <- toHexDigit (int buf.[i] &&& 0xF)
    n <- n + 1
  if prefix then String.Concat("0x", new String(hex)) 
  else new String(hex)
        
[<CompiledName("Decode")>]
let decode (s:string) =
  match s with
  | null -> nullArg "s"
  | _ when s.Length = 0 -> Array.empty
  | _ ->
      let mutable len = s.Length
      let mutable i = 0
      if len >= 2 && s.[0] = '0' && (s.[1] = 'x' || s.[1] = 'X') then do
        len <- len - 2
        i <- i + 2
      if len % 2 <> 0 then invalidArg "s" "Invalid hex format"
      else
        let buf = Array.zeroCreate (len / 2)
        let mutable n = 0
        while i < s.Length do
          buf.[n] <- byte (((fromHexDigit s.[i]) <<< 4) ||| (fromHexDigit s.[i + 1]))
          i <- i + 2
          n <- n + 1
        buf
