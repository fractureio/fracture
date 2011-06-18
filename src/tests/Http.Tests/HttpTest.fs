module Fracture.Http.Tests.HttpTest

open System
open FParsec.Error
open FParsec.CharParsers
open Fracture.Http.Primitives
open Fracture.Http.CharParsers
open Fracture.Http.Uri
open Fracture.Http.Http
open NUnit.Framework
open FsUnit

let run p input =
  match run p input with
  | Success(actual,_,_) -> actual
  | Failure(error,_,_) -> failwith error

[<TestCase(" ", Result=' ')>]
[<TestCase("\t", Result=' ')>]
[<TestCase("\n ", Result=' ')>]
[<TestCase("\r ", Result=' ')>]
[<TestCase("\r\n ", Result=' ')>]
[<TestCase("\n  ", Result=' ')>]
[<TestCase("\r  ", Result=' ')>]
[<TestCase("\r\n  ", Result=' ')>]
[<TestCase("\n\t", Result=' ')>]
[<TestCase("\r\t", Result=' ')>]
[<TestCase("\r\n\t", Result=' ')>]
[<TestCase("\n\t\t", Result=' ')>]
[<TestCase("\r\t\t", Result=' ')>]
[<TestCase("\r\n\t\t", Result=' ')>]
[<TestCase("\n \t", Result=' ')>]
[<TestCase("\r \t", Result=' ')>]
[<TestCase("\r\n \t", Result=' ')>]
[<TestCase("\n\t ", Result=' ')>]
[<TestCase("\r\t ", Result=' ')>]
[<TestCase("\r\n\t ", Result=' ')>]
[<TestCase("", ExpectedException=typeof<Exception>)>]
[<TestCase("\n", ExpectedException=typeof<Exception>)>]
[<TestCase("\r", ExpectedException=typeof<Exception>)>]
[<TestCase("\r\n", ExpectedException=typeof<Exception>)>]
let ``test lws should parse one or more white spaces, including an optional line fold``(input) = run lws input
