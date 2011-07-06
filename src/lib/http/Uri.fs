module Fracture.Http.Uri

open System
open FParsec.Primitives
open FParsec.CharParsers
open Primitives
open CharParsers

type UriKind =
  | AbsoluteUri  of UriPart list
  | RelativeUri  of UriPart list
  | UriAuthority of UriPart list
  | FragmentRef  of UriPart
  | AnyUri
and UriPart =
  | Scheme      of string
  | UserInfo    of string
  | Host        of string
  | Port        of string
  | Path        of string
  | QueryString of string
  | Fragment    of string

let mark<'a> : Parser<char,'a> = anyOf "-_.!~*'()"
let reserved<'a> : Parser<char,'a> = anyOf ";/?:@&=+$,"
let unreserved<'a> : Parser<char,'a> = asciiLetter <|> digit <|> mark
let paramChar<'a> : Parser<char,'a> = unreserved <|> escaped <|> anyOf ":@&=+$."
let uriChar<'a> : Parser<char,'a> = reserved <|> unreserved <|> escaped
let uriCharNoSlash<'a> : Parser<char,'a> = unreserved <|> escaped <|> anyOf ";?:@&=+$,"

let relSegment<'a> : Parser<char list,'a> = unreserved <|> escaped <|> anyOf ";@&=+$," |> many1
let regName<'a> : Parser<char list,'a> = unreserved <|> escaped <|> anyOf "$,;:@&=+" |> many1
let userInfo<'a> : Parser<char list,'a> = unreserved <|> escaped <|> anyOf ";:&=+$," |> many

let param<'a> : Parser<char list,'a> = many paramChar
let segment<'a> : Parser<char list,'a> = flatten <!> param <*> many (cons <!> semicolon <*> param)
let pathSegments<'a> : Parser<char list,'a> = flatten <!> segment <*> many (cons <!> slash <*> segment)
let uriAbsPath<'a> : Parser<char list,'a> = cons <!> slash <*> pathSegments

let relPath<'a> : Parser<char list,'a> =
  pipe2 relSegment (opt uriAbsPath) <| fun hd tl -> match tl with | Some(t) -> hd @ t | _ -> hd

let uriQuery<'a> : Parser<char list,'a> = many uriChar
let uriFragment<'a> : Parser<char list,'a> = many uriChar

let ipWithDot<'a> : Parser<char list,'a> = many1 digit >>= (fun a -> dot >>= (fun b -> preturn (a @ [b])))
let ipv4Address<'a> : Parser<char list,'a> =  pipe2 (parray 3 ipWithDot) (many1 digit) <| fun a b -> (List.concat a) @ b
// TODO: prevent a trailing hyphen
let topLabel<'a> : Parser<char list,'a> = pipe2 asciiLetter (many (alphanum <|> hyphen)) cons <|> listify asciiLetter 
let domainLabel<'a> : Parser<char list,'a> = pipe2 alphanum (many (alphanum <|> hyphen)) cons <|> listify alphanum
let domain<'a> : Parser<char list,'a> = pipe2 domainLabel dot <| fun a b -> a @ [b]
let hostname<'a> : Parser<char list,'a> = pipe2 (opt domain) (topLabel .>> opt dot) <| fun a b -> match a with Some(sub) -> sub @ b | _ -> b

let host<'a> : Parser<char list,'a> = hostname <|> ipv4Address
let port<'a> : Parser<char list,'a> = many digit

// Start returning UriParts
let scheme<'a> : Parser<UriPart,'a> = pipe2 asciiLetter (many1 (choice [asciiLetter; digit; plus; hyphen; dot])) <| fun a b -> Scheme !!(a::b)
let hostport<'a> : Parser<UriPart list,'a> =
  pipe2 host (opt (colon >>. port)) <| fun a b -> match b with Some(v) -> [Host !!a; Port !!v] | _ -> [Host !!a]
let server<'a> : Parser<UriPart list,'a> =
  pipe2 (opt (userInfo .>> at)) hostport <| fun a b -> match a with Some(info) -> (UserInfo !!info)::b | _ -> b
let uriAuthority<'a> : Parser<UriPart list,'a> = regName |>> (fun a -> [Host !!a]) <|> server
let netPath<'a> : Parser<UriPart list,'a> =
  pipe2 (skipString "//" >>. uriAuthority) (opt uriAbsPath) <| fun a b -> a @ [Path(match b with Some(path) -> !!path | _ -> "/")]
let opaquePart<'a> : Parser<UriPart list,'a> = pipe2 uriCharNoSlash (many uriChar) <| fun u1 u2 -> [Host !!(u1::u2)]
let hierPart<'a> : Parser<UriPart list,'a> =
  pipe3 (netPath <|> ((fun a -> [Path !!a]) <!> uriAbsPath)) (opt (qmark *> uriQuery)) (opt (hash *> uriFragment))
  <| fun a b c -> a @ [QueryString(match b with Some(q) -> !!q | _ -> null);Fragment(match c with Some(f) -> !!f | _ -> null)]

let absoluteUri<'a> : Parser<UriKind,'a> = (fun a b -> AbsoluteUri(a::b)) <!> scheme .>> colon <*> (hierPart <|> opaquePart)
let relativeUri<'a> : Parser<UriKind,'a> =
  pipe3 (uriAbsPath <|> relPath) (opt (qmark *> uriQuery)) (opt (hash *> uriFragment))
  <| fun a b c -> RelativeUri [Path !!a;QueryString(match b with Some(q) -> !!q | _ -> null);Fragment(match c with Some(f) -> !!f | _ -> null)]

let authorityRef<'a> : Parser<UriKind, 'a> = uriAuthority |>> UriAuthority
let fragmentRef<'a> : Parser<UriKind,'a> =  hash >>. uriFragment |>> fun f -> FragmentRef(Fragment !!f)
let uriReference<'a> : Parser<UriKind,'a> = absoluteUri <|> relativeUri <|> fragmentRef
let anyUri<'a> : Parser<UriKind, 'a> = pstring "*" |>> fun _ -> AnyUri
