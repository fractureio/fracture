module HttpParser.Http

open System
open FParsec.Primitives
open FParsec.CharParsers
open HttpParser.Primitives
open HttpParser.CharParsers
open HttpParser.Uri

type HttpRequestMessageParseEvent =
  | RequestMessageBegin
  | RequestMethod of char list
  | RequestUri of char list
  | QueryString of char list
  | Fragment of char list
  | RequestHeaderName of char list
  | RequestHeaderValue of char list
  | RequestHeadersEnd
  | RequestBody of char list
  | RequestMessageEnd

type HttpResponseMessageParseEvent =
  | ResponseMessageBegin
  | StatusCode of char list
  | StatusDescription of char list
  | ResponseHeaderName of char list
  | ResponseHeaderValue of char list
  | ResponseHeadersEnd
  | ResponseBody of char list
  | ResponseMessageEnd

// Basic Rules
let lws<'a> : Parser<char, 'a> = (fun _ _ -> ' ') <!> opt newline <*> many1 (space <|> tab)
  