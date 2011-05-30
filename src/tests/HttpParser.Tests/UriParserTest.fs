module HttpParser.Tests.UriParserTest

open System
open FParsec.Error
open FParsec.CharParsers
open HttpParser.CharParsers
open HttpParser.UriParser
open NUnit.Framework
open FsUnit

let ``scheme cases`` = [|
  [| box ""                    ; box (Scheme null)       |]
  [| box "http"                ; box (Scheme "http")     |]
  [| box "http://boo"          ; box (Scheme "http")     |]
  [| box "https"               ; box (Scheme "https")    |]
  [| box "https://boo"         ; box (Scheme "https")    |]
  [| box "urn"                 ; box (Scheme "urn")      |]
  [| box "urn:"                ; box (Scheme "urn")      |]
  [| box "ftp"                 ; box (Scheme "ftp")      |]
  [| box "ftp:"                ; box (Scheme "ftp")      |]
  [| box "mailto"              ; box (Scheme "mailto")   |]
  [| box "mailto:ryan@owin.org"; box (Scheme "mailto")   |]
  [| box "owin.org"            ; box (Scheme "owin.org") |]
  [| box "owin:org"            ; box (Scheme "owin")     |]
  [| box "owin.org:"           ; box (Scheme "owin.org") |]
|]

[<Test>]
[<TestCaseSource("scheme cases")>]
let ``Given a scheme, the parser should return a Scheme``(input, expected: UriPart) =
  match run scheme input with
  | Success(actual,_,_) -> actual   |> should equal expected
  | _                   -> expected |> should equal (Scheme null)

let ``hostport cases`` = [|
  [| box ""                  ; box [Host null] |]
  [| box ":80"               ; box [Host null] |]
  [| box "owin.org"          ; box [Host "owin.org"] |]
  [| box "owin.org/"         ; box [Host "owin.org"] |]
  [| box "owin.org/blah"     ; box [Host "owin.org"] |]
  [| box "owin.org:"         ; box [Host "owin.org"; Port ""] |]
  [| box "owin.org:80"       ; box [Host "owin.org"; Port "80"] |]
  [| box "owin.org:80/"      ; box [Host "owin.org"; Port "80"] |]
  [| box "owin.org:80/blah"  ; box [Host "owin.org"; Port "80"] |]
  [| box "owin.org:8080"     ; box [Host "owin.org"; Port "8080"] |]
  [| box "owin.org:8080/"    ; box [Host "owin.org"; Port "8080"] |]
  [| box "owin.org:8080/blah"; box [Host "owin.org"; Port "8080"] |]
  [| box "192.168.0.1"       ; box [Host null] |]
  [| box "192.168.0.1/"      ; box [Host null] |]
  [| box "192.168.0.1:80"    ; box [Host null] |]
  [| box "192.168.0.1:80/"   ; box [Host null] |]
|]

[<Test>]
[<TestCaseSource("hostport cases")>]
let ``Given a hostport, the parser should return a list of Host and Port``(input, expected: UriPart list) =
  match run hostport input with
  | Success(actual,_,_) -> actual   |> should equal expected
  | _                   -> expected |> should equal [Host null]
