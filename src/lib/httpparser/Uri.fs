module HttpParser.Uri

open System
open FParsec.Primitives
open FParsec.CharParsers
open HttpParser.Primitives
open HttpParser.CharParsers

type UriKind =
  | AbsoluteUri of UriPart list
  | RelativeUri of UriPart list
  | FragmentRef of UriPart
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
let unreserved<'a> : Parser<char,'a> = asciiLetter <|> mark
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

let relPath<'a> : Parser<char list,'a> = (fun hd tl -> match tl with | Some(t) -> hd @ t | _ -> hd) <!> relSegment <*> opt uriAbsPath

let uriQuery<'a> : Parser<char list,'a> = many uriChar
let uriFragment<'a> : Parser<char list,'a> = many uriChar

let ipWithDot<'a> : Parser<char list,'a> = many1 digit >>= (fun a -> dot >>= (fun b -> preturn (a @ [b])))
let ipv4Address<'a> : Parser<char list,'a> = (fun a b -> (List.concat a) @ b) <!> parray 3 ipWithDot <*> many1 digit
// TODO: prevent a trailing hyphen
let topLabel<'a> : Parser<char list,'a> = (cons <!> asciiLetter <*> many (alphanum <|> hyphen)) <|> listify asciiLetter 
let domainLabel<'a> : Parser<char list,'a> = (cons <!> alphanum <*> many (alphanum <|> hyphen)) <|> listify alphanum
let domain<'a> : Parser<char list,'a> = (fun a b -> a @ [b]) <!> domainLabel <*> dot
let hostname<'a> : Parser<char list,'a> = (fun a b -> match a with Some(sub) -> sub @ b | _ -> b) <!> opt domain <*> (topLabel .>> opt dot)

let host<'a> : Parser<char list,'a> = hostname <|> ipv4Address
let port<'a> : Parser<char list,'a> = many digit

// Start returning UriParts
let scheme<'a> : Parser<UriPart,'a> = (fun a b -> Scheme !!(a::b)) <!> asciiLetter <*> many1 (choice [asciiLetter; digit; plus; hyphen; dot])
let hostport<'a> : Parser<UriPart list,'a> =
  (fun a b -> match b with Some(v) -> [Host !!a; Port !!v] | _ -> [Host !!a]) <!> host <*> opt (colon >>. port)
let server<'a> : Parser<UriPart list,'a> =
  (fun a b -> match a with Some(info) -> (UserInfo !!info)::b | _ -> b) <!> opt (userInfo .>> pchar '@') <*> hostport
let uriAuthority<'a> : Parser<UriPart list,'a> = (fun a -> Host !!a) <!> regName |> listify <|> server
let netPath<'a> : Parser<UriPart list,'a> =
  (fun a b -> match b with Some(path) -> a @ [Path !!path] | _ -> a) <!> skipString "//" *> uriAuthority <*> opt uriAbsPath

let opaquePart<'a> : Parser<UriPart list,'a> = (fun u1 u2 -> [Host !!(u1::u2)]) <!> uriCharNoSlash <*> many uriChar
let hierPart<'a> : Parser<UriPart list,'a> =
  (fun a b c -> a @ [QueryString(match b with Some(q) -> !!q | _ -> null);Fragment(match c with Some(f) -> !!f | _ -> null)])
  <!> (netPath <|> ((fun a -> [Path !!a]) <!> uriAbsPath))
  <*> opt (cons <!> qmark <*> uriQuery)
  <*> opt (cons <!> hash <*> uriFragment)

let absoluteUri<'a> : Parser<UriKind,'a> = (fun a b -> AbsoluteUri(a::b)) <!> scheme .>> colon <*> (hierPart <|> opaquePart)
let relativeUri<'a> : Parser<UriKind,'a> =
  (fun a b c -> RelativeUri [Path !!a;QueryString(match b with Some(q) -> !!q | _ -> null);Fragment(match c with Some(f) -> !!f | _ -> null)])
  <!> (uriAbsPath <|> relPath)
  <*> opt (cons <!> qmark <*> uriQuery)
  <*> opt (cons <!> hash <*> uriFragment)

let fragmentRef<'a> : Parser<UriKind,'a> = (fun a b -> FragmentRef(Fragment !!(a::b))) <!> hash <*> uriFragment
let uriReference<'a> : Parser<UriKind,'a> = absoluteUri <|> relativeUri <|> fragmentRef
