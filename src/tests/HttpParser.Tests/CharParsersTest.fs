module HttpParser.Tests.CharParsersTest

open System
open FParsec.CharParsers
open HttpParser.Hex
open HttpParser.Primitives
open HttpParser.CharParsers
open NUnit.Framework
open FsUnit

[<TestCase("%20"," ")>]
[<TestCase("%2F","/")>]
[<TestCase("%2f","/")>]
let ``test escaped parser returns the byte representing the full hexadecimal character``(esc, expected) =
  match run escaped esc with
  | Success(actual,_,_) -> string actual |> should equal expected
  | Failure(error,_,_) -> failwith error
