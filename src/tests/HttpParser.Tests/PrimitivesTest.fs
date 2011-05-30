module HttpParser.Tests.PrimitivesTest

open System
open FParsec.CharParsers
open HttpParser.Hex
open HttpParser.Primitives
open NUnit.Framework
open FsUnit

[<TestCase('\n',"\n",'\n')>]
[<TestCase('\r',"\r",'\r')>]
let ``test pchar '\r' and '\n' should return '\n'``(charToParse, input, expected) =
  let p = pchar charToParse
  match run p input with
  | Success(actual,_,_) -> actual |> should equal expected
  | Failure(error,_,_) -> failwith error

[<TestCase("\n",'\n')>]
[<TestCase("\r",'\n')>]
[<TestCase("\r\n",'\n')>]
let ``test newline should parse all newline chars``(input, expected) =
  match run newline input with
  | Success(actual,_,_) -> actual |> should equal expected
  | Failure(error,_,_) -> failwith error

[<TestCase('a','b',"ab","ab")>]
[<TestCase('/','/',"//","//")>]
[<TestCase(' ',' ',"  ","  ")>]
let ``test listify2 should run two parsers and put the results in a list``(x, y, input, expected) =
  let expected = String.toCharList expected
  let p = listify2 (pchar x) (pchar y)
  match run p input with
  | Success(actual,_,_) -> actual |> should equal expected
  | Failure(error,_,_) -> failwith error