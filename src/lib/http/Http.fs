module Fracture.Http.Http

open System
open System.Xml.Linq
open FParsec.Primitives
open FParsec.CharParsers
open Primitives
open CharParsers
open Uri

type HttpRequestMethod =
  | OPTIONS
  | GET
  | HEAD
  | POST
  | PUT
  | DELETE
  | TRACE
  | CONNECT
  | ExtensionMethod of string

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

// TODO: Basic Rules
let lws<'a> : Parser<char, 'a> = (fun _ _ -> ' ') <!> opt newline <*> many1 (space <|> tab)
let internal separatorChars = "()<>@,;:\\\"/[]?={} \t"
let separators<'a> : Parser<char, 'a> = anyOf separatorChars
let token<'a> : Parser<string, 'a> =
  many1Satisfy2 isAsciiLetter (isNoneOf (controlChars + separatorChars))

// HTTP Request Method
let internal poptions<'a> : Parser<string, 'a> = pstring "OPTIONS"
let internal pget<'a> : Parser<string, 'a> = pstring "GET"
let internal phead<'a> : Parser<string, 'a> = pstring "HEAD"
let internal ppost<'a> : Parser<string, 'a> = pstring "POST"
let internal pput<'a> : Parser<string, 'a> = pstring "PUT"
let internal pdelete<'a> : Parser<string, 'a> = pstring "DELETE"
let internal ptrace<'a> : Parser<string, 'a> = pstring "TRACE"
let internal pconnect<'a> : Parser<string, 'a> = pstring "CONNECT"
let internal mapHttpMethod = function
  | "OPTIONS" -> OPTIONS
  | "GET" -> GET
  | "HEAD" -> HEAD
  | "POST" -> POST
  | "PUT" -> PUT
  | "DELETE" -> DELETE
  | "TRACE" -> TRACE
  | "CONNECT" -> CONNECT
  | x -> ExtensionMethod x
let httpMethod<'a> : Parser<HttpRequestMethod, 'a> =
  mapHttpMethod <!> (poptions <|> pget <|> phead <|> ppost <|> pput <|> pdelete <|> ptrace <|> pconnect <|> token)

// HTTP Request URI
let httpRequestUri<'a> : Parser<UriKind, 'a> = anyUri <|> absoluteUri <|> relativeUri <|> authorityRef

// HTTP version
let skipHttpPrefix<'a> : Parser<unit, 'a> = skipString "HTTP/"
let skipDot<'a> : Parser<unit, 'a> = skipChar '.'
let httpVersion<'a> : Parser<HttpVersion, 'a> =
  (fun major minor -> HttpVersion(major, minor)) <!> (skipHttpPrefix >>. pint32) <*> (skipDot >>. pint32)

// HTTP Request Line
let httpRequestLine<'a> : Parser<HttpRequestLine, 'a> = 
  (fun x y z -> HttpRequestLine(x,y,z))
  <!> (httpMethod .>> skipSpace)
  <*> (httpRequestUri .>> skipSpace)
  <*> (httpVersion .>> skipNewline)
