module HttpParser.Http

open System
open System.Xml.Linq
open FParsec.Primitives
open FParsec.CharParsers
open HttpParser.Primitives
open HttpParser.CharParsers
open HttpParser.Uri

type HttpRequestMethod =
  | GET
  | POST
  | PUT
  | DELETE
  | HEAD
  | OPTIONS
  // ... more to come

type HttpVersion = HttpVersion of int * int

type HttpRequestLine = HttpRequestLine of HttpRequestMethod * UriKind * HttpVersion

type HttpResponseStatusLine = HttpResponseStatusLine of int * string

type HttpHeader = HttpHeader of string * string list

type HttpMessageBody =
  | EmptyBody
  | UrlFormEncodedBody of (string * string) list
  | XmlBody of XElement
  | JsonBody of string
  | StringBody of string
  | ByteArrayBody of byte[]

type HttpMessage =
  | HttpRequestMessage of HttpRequestLine * HttpHeader list * HttpMessageBody
  | HttpResponseMessage of HttpResponseStatusLine * HttpHeader list * HttpMessageBody

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
  