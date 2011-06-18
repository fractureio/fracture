module Fracture.Http.CharParsers

open System
open FParsec.Primitives
open FParsec.CharParsers
open Primitives

let control<'a> : Parser<char,'a> = anyOf (String(Array.map char [| 0uy..31uy |])) <|> pchar (char 127uy)
let alphanum<'a> : Parser<char, 'a> = asciiLetter <|> digit
let space<'a> : Parser<char, 'a> = pchar ' '
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
let escaped<'a> : Parser<char, 'a> = readHex <!> skipChar '%' *> hex <*> hex
