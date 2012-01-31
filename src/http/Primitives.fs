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
