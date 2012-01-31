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