module Fracture.Http.Http

open System
open System.Xml.Linq
open FParsec
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
type HttpStatusLine = HttpStatusLine of int * string
type HttpHeader = HttpHeader of string * string

type IHttpHeader =
  abstract IsGeneralHeader : bool
type IHttpRequestHeader =
  abstract IsRequestHeader : bool
type IHttpResponseHeader =
  abstract IsResponseHeader : bool

type HttpGeneralHeader =
  | CacheControl of string
  | Connection of string
  | Date of string
  | Pragma of string
  | Trailer of string
  | TransferEncoding of string
  | Upgrade of string
  | Via of string
  | Warning of string
  interface IHttpHeader with member x.IsGeneralHeader = true
  interface IHttpRequestHeader with member x.IsRequestHeader = false
  interface IHttpResponseHeader with member x.IsResponseHeader = false

type HttpRequestHeader =
  | Accept of string
  | AcceptCharset of string
  | AcceptEncoding of string
  | AcceptLanguage of string
  | Authorization of string
  | Expect of string
  | From of string
  | Host of string
  | IfMatch of string
  | IfModifiedSince of string
  | IfNoneMatch of string
  | IfRange of string
  | IfUnmodifiedSince of string
  | MaxForwards of string
  | ProxyAuthorization of string
  | Range of string
  | Referrer of string
  | TE of string
  | UserAgent of string
  interface IHttpHeader with member x.IsGeneralHeader = false
  interface IHttpRequestHeader with member x.IsRequestHeader = true

type HttpMessageBody =
  | EmptyBody
  | UrlFormEncodedBody of (string * string) list
  | XmlBody of XElement
  | JsonBody of string
  | StringBody of string
  | ByteArrayBody of byte[]

type HttpMessage =
  | HttpRequestMessage of HttpRequestLine * HttpHeader list * HttpMessageBody
  | HttpResponseMessage of HttpStatusLine * HttpHeader list * HttpMessageBody

type IHttpMessageParserHandler =
  abstract OnHttpMessageBegin : unit -> unit
  // TODO: make this more generic to also process responses
  abstract OnStartLine : HttpRequestLine -> unit
  abstract OnHeader : HttpHeader -> unit
  abstract OnHeadersEnd : unit -> unit
  abstract OnBody : HttpMessageBody -> unit
  abstract OnHttpMessageEnd : unit -> unit
  // TODO: Possibly call ToString on ParserError and wrap in an exception?
  abstract OnError : ParserError -> unit

let text<'a> : Parser<char, 'a> = noneOf controlChars
let lws<'a> : Parser<char, 'a> = optional skipNewline >>. many1 (space <|> tab) >>% ' '
let internal separatorChars = "()<>@,;:\\\"/[]?={} \t"
let separators<'a> : Parser<char, 'a> = anyOf separatorChars
let token<'a> : Parser<string, 'a> =
  many1Satisfy2 isAsciiLetter (isNoneOf (controlChars + separatorChars))
let quotedPair<'a> : Parser<char, 'a> = skipChar '\\' >>. anyChar

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
  poptions <|> pget <|> phead <|> ppost <|> pput <|> pdelete <|> ptrace <|> pconnect <|> token |>> mapHttpMethod

// HTTP Request URI
let httpRequestUri<'a> : Parser<UriKind, 'a> = anyUri <|> absoluteUri <|> relativeUri <|> authorityRef

// HTTP version
let skipHttpPrefix<'a> : Parser<unit, 'a> = skipString "HTTP/"
let skipDot<'a> : Parser<unit, 'a> = skipChar '.'
let httpVersion<'a> : Parser<HttpVersion, 'a> =
  pipe2 (skipHttpPrefix >>. pint32) (skipDot >>. pint32) <| fun major minor -> HttpVersion(major, minor)

// HTTP Request Line
let httpRequestLine : Parser<HttpRequestLine, IHttpMessageParserHandler> = 
  pipe4 (httpMethod .>> skipSpace) (httpRequestUri .>> skipSpace) (httpVersion .>> skipNewline) getUserState
  <| fun x y z u ->
       let startLine = HttpRequestLine(x, y, z)
       u.OnStartLine(startLine)
       startLine

// HTTP Status Line
let httpStatusLine<'a> : Parser<HttpStatusLine, 'a> = 
  pipe3 (pint32 .>> skipSpace) (many1 text .>> skipNewline) getUserState
  <| fun x y u -> HttpStatusLine(x, String.ofCharList y)

// HTTP Headers
let skipColon<'a> : Parser<unit, 'a> = skipChar ':'
// TODO: This can be improved "by consisting of either *TEXT or combinations of token, separators, and quoted-string"
let fieldContent<'a> : Parser<char, 'a> = text <|> attempt lws
let httpHeader : Parser<HttpHeader, IHttpMessageParserHandler> =
  pipe3 (token .>> skipColon) (many fieldContent .>> skipNewline) getUserState
  <| fun x y u ->
       let header = HttpHeader(x, (String.ofCharList y).TrimWhiteSpace())
       u.OnHeader(header)
       header

// HTTP Message Body
// TODO: create a real body parser
let httpMessageBody : Parser<HttpMessageBody, IHttpMessageParserHandler> =
  pipe2 (preturn EmptyBody) getUserState <| fun x u -> u.OnBody(x); x

/// Skips the \r\n newline marker separating headers from the message body.
let skipBodySeparator : Parser<unit, IHttpMessageParserHandler> =
  let error = expected "newline (\\r\\n)"
  let crcn = TwoChars('\r','\n')
  fun stream ->
    if stream.Skip(crcn) then  // if this is too aggressive, we can fall back to the built-in stream.SkipNewline()
      stream.RegisterNewline()
      let handler = stream.State.UserState
      handler.OnHeadersEnd()
      Reply(())
    else Reply(Error, error)

// HTTP Request Message
let httpRequestMessage : Parser<HttpMessage, IHttpMessageParserHandler> =
  pipe4 httpRequestLine (many httpHeader) skipBodySeparator httpMessageBody (fun w x _ z -> HttpRequestMessage(w, x, z))
