module Fracture.Http.Primitives

open System
open FParsec.Primitives
open Fracture.Http.Hex

module String =
  let ofCharList (input:char list) = String(Array.ofList input)
  let toCharList (input:String) = input.ToCharArray() |> Array.toList

let inline (<*>) f a = f >>= fun f' -> a >>= fun a' -> preturn (f' a')
let inline lift f a = a |>> f
let inline (<!>) f a = lift f a
let inline lift2 f a b = pipe2 a b f  // preturn f <*> a <*> b
let inline ( *>) x y = x >>. y        // lift2 (fun _ z -> z) x y
let inline ( <*) x y = x .>> y        // lift2 (fun z _ -> z) x y
let inline ( !!) s = String.ofCharList s
let readHex a b = fromHexDigit a <<< 4 ||| fromHexDigit b |> char
let listify a = (fun x -> [x]) <!> a
let listify2 a b = (fun a b -> a::[b]) <!> a <*> b
let cons hd tl = hd::tl
let flatten hd tl = hd::tl |> List.concat
