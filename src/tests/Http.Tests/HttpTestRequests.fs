module Fracture.Http.Tests.HttpTestRequests

open System
open System.Collections.Generic
open System.Text

type TestRequest = {
    Name: string
    Raw: byte[]
    Method: string
    RequestUri: string
    RequestPath: string
    QueryString: string
    Fragment: string
    VersionMajor: int
    VersionMinor: int
    Headers: IDictionary<string, string>
    Body: byte[] 
    ShouldKeepAlive: bool // if the message is 1.1 and !Connection:close or message is < 1.1 and Connection:keep-alive
    OnHeadersEndCalled: bool }

let requests = [|
  { Name = "No headers no body"
    Raw = "\r\nGET /foo HTTP/1.1\r\n\r\n"B
    Method = "GET"
    RequestUri = "/foo"
    RequestPath = "/foo"
    QueryString = null
    Fragment = null
    VersionMajor = 1
    VersionMinor = 1
    Headers = dict Seq.empty
    OnHeadersEndCalled = false
    Body = null
    ShouldKeepAlive = true }
  { Name = "No headers no body no version"
    Raw = "GET /foo\r\n\r\n"B
    Method = "GET"
    RequestUri = "/foo"
    RequestPath = "/foo"
    QueryString = null
    Fragment = null
    VersionMajor = 0
    VersionMinor = 9
    Headers = dict Seq.empty
    OnHeadersEndCalled = false
    Body = null
    ShouldKeepAlive = false }
  { Name = "no body"
    Raw = "GET /foo HTTP/1.1\r\nFoo: Bar\r\n\r\n"B
    Method = "GET"
    RequestUri = "/foo"
    RequestPath = "/foo"
    QueryString = null
    Fragment = null
    VersionMajor = 1
    VersionMinor = 1
    Headers = dict [( "Foo", "Bar" )]
    OnHeadersEndCalled = false
    Body = null
    ShouldKeepAlive = true }
  { Name = "no body no version"
    Raw = "GET /foo\r\nFoo: Bar\r\n\r\n"B
    Method = "GET"
    RequestUri = "/foo"
    RequestPath = "/foo"
    QueryString = null
    Fragment = null
    VersionMajor = 0
    VersionMinor = 9
    Headers = dict [("Foo", "Bar" )]
    OnHeadersEndCalled = false
    Body = null
    ShouldKeepAlive = false }
  { Name = "query string"
    Raw = "GET /foo?asdf=jklol HTTP/1.1\r\nFoo: Bar\r\nBaz-arse: Quux\r\n\r\n"B
    Method = "GET"
    RequestUri = "/foo?asdf=jklol"
    RequestPath = "/foo"
    QueryString = "asdf=jklol"
    Fragment = null
    VersionMajor = 1
    VersionMinor = 1
    Headers = dict [( "Foo", "Bar" ); ( "Baz-arse", "Quux" )]
    OnHeadersEndCalled = false
    Body = null
    ShouldKeepAlive = true }
  { Name = "fragment"
    Raw = "POST /foo?asdf=jklol#poopz HTTP/1.1\r\nFoo: Bar\r\nBaz: Quux\r\n\r\n"B
    Method = "POST"
    RequestUri = "/foo?asdf=jklol#poopz"
    RequestPath = "/foo"
    QueryString = "asdf=jklol"
    Fragment = "poopz"
    VersionMajor = 1
    VersionMinor = 1
    Headers = dict [( "Foo", "Bar" ); ( "Baz", "Quux" )]
    OnHeadersEndCalled = false
    Body = null
    ShouldKeepAlive = true }
  { Name = "zero content length"
    Raw = "POST /foo HTTP/1.1\r\nFoo: Bar\r\nContent-Length: 0\r\n\r\n"B
    Method = "POST"
    RequestUri = "/foo"
    RequestPath = "/foo"
    QueryString = null
    Fragment = null
    VersionMajor = 1
    VersionMinor = 1
    Headers = dict [( "Foo", "Bar" ); ( "Content-Length", "0" )]
    OnHeadersEndCalled = false
    Body = null
    ShouldKeepAlive = true }
  { Name = "some content length"
    Raw = "POST /foo HTTP/1.1\r\nFoo: Bar\r\nContent-Length: 5\r\n\r\nhello"B
    Method = "POST"
    RequestUri = "/foo"
    RequestPath = "/foo"
    QueryString = null
    Fragment = null
    VersionMajor = 1
    VersionMinor = 1
    Headers = dict [( "Foo", "Bar" ); ( "Content-Length", "5" )]
    OnHeadersEndCalled = false
    Body = Encoding.UTF8.GetBytes("hello")
    ShouldKeepAlive = true }
  { Name = "1.1 get"
    Raw = "GET /foo HTTP/1.1\r\nFoo: Bar\r\nConnection: keep-alive\r\n\r\n"B
    Method = "GET"
    RequestUri = "/foo"
    RequestPath = "/foo"
    QueryString = null
    Fragment = null
    VersionMajor = 1
    VersionMinor = 1
    Headers = dict [( "Foo", "Bar" ); ( "Connection", "keep-alive" )]
    OnHeadersEndCalled = false
    Body = null
    ShouldKeepAlive = true }
  { Name = "1.1 get close"
    Raw = "GET /foo HTTP/1.1\r\nFoo: Bar\r\nConnection: CLOSE\r\n\r\n"B
    Method = "GET"
    RequestUri = "/foo"
    RequestPath = "/foo"
    QueryString = null
    Fragment = null
    VersionMajor = 1
    VersionMinor = 1
    Headers = dict [( "Foo", "Bar" ); ( "CoNNection", "CLOSE" )]
    OnHeadersEndCalled = false
    Body = null
    ShouldKeepAlive = false }
  { Name = "1.1 post"
    Raw = "POST /foo HTTP/1.1\r\nFoo: Bar\r\nContent-Length: 15\r\n\r\nhelloworldhello"B
    Method = "POST"
    RequestUri = "/foo"
    RequestPath = "/foo"
    QueryString = null
    Fragment = null
    VersionMajor = 1
    VersionMinor = 1
    Headers = dict [( "Foo", "Bar" ); ( "Content-Length", "15" )]
    OnHeadersEndCalled = false
    Body = Encoding.UTF8.GetBytes("helloworldhello")
    ShouldKeepAlive = true }
  { Name = "1.1 post close"
    Raw = "POST /foo HTTP/1.1\r\nFoo: Bar\r\nContent-Length: 15\r\nConnection: close\r\nBaz: Quux\r\n\rldhello"B
    Method = "POST"
    RequestUri = "/foo"
    RequestPath = "/foo"
    QueryString = null
    Fragment = null
    VersionMajor = 1
    VersionMinor = 1
    Headers = dict [( "Foo", "Bar" ); ( "Content-Length", "15" ); ( "Connection", "close" ); ( "Baz", "Quux" )]
    OnHeadersEndCalled = false
    Body = Encoding.UTF8.GetBytes("helloworldhello")
    ShouldKeepAlive = false }
  // because it has no content-length it's not keep alive anyway? TODO 
  { Name = "get connection close"
    Raw = "GET /foo?asdf=jklol#poopz HTTP/1.1\r\nFoo: Bar\r\nBaz: Quux\r\nConnection: close\r\n\r\n"B
    Method = "GET"
    RequestUri = "/foo?asdf=jklol#poopz"
    RequestPath = "/foo"
    QueryString = "asdf=jklol"
    Fragment = "poopz"
    VersionMajor = 1
    VersionMinor = 1
    Headers = dict [( "Foo", "Bar" ); ( "Baz", "Quux" ); ( "Connection", "close" )]
    OnHeadersEndCalled = false
    Body = null
    ShouldKeepAlive = false }
  { Name = "1.0 get"
    Raw = "GET /foo?asdf=jklol#poopz HTTP/1.0\r\nFoo: Bar\r\nBaz: Quux\r\n\r\n"B
    Method = "GET"
    RequestUri = "/foo?asdf=jklol#poopz"
    RequestPath = "/foo"
    QueryString = "asdf=jklol"
    Fragment = "poopz"
    VersionMajor = 1
    VersionMinor = 0
    Headers = dict [( "Foo", "Bar" ); ( "Baz", "Quux" )]
    OnHeadersEndCalled = false
    Body = null
    ShouldKeepAlive = false }
  { Name = "1.0 get keep-alive"
    Raw = "GET /foo?asdf=jklol#poopz HTTP/1.0\r\nFoo: Bar\r\nBaz: Quux\r\nConnection: keep-alive\r\n\r\n"B
    Method = "GET"
    RequestUri = "/foo?asdf=jklol#poopz"
    RequestPath = "/foo"
    QueryString = "asdf=jklol"
    Fragment = "poopz"
    VersionMajor = 1
    VersionMinor = 0
    Headers = dict [( "Foo", "Bar" ); ( "Baz", "Quux" ); ( "Connection", "keep-alive" )]
    OnHeadersEndCalled = false
    Body = null
    ShouldKeepAlive = true }
  { Name = "1.0 post"
    Raw = "POST /foo HTTP/1.0\r\nFoo: Bar\r\n\r\nhelloworldhello"B
    Method = "POST"
    RequestUri = "/foo"
    RequestPath = "/foo"
    QueryString = null
    Fragment = null
    VersionMajor = 1
    VersionMinor = 0
    Headers = dict [( "Foo", "Bar" )]
    OnHeadersEndCalled = false
    Body = Encoding.UTF8.GetBytes("helloworldhello")
    ShouldKeepAlive = false }
  { Name = "1.0 post keep-alive with content length"
    Raw = "POST /foo HTTP/1.0\r\nContent-Length: 15\r\nFoo: Bar\r\nConnection: keep-alive\r\n\r\nhelloworldhello"B
    Method = "POST"
    RequestUri = "/foo"
    RequestPath = "/foo"
    QueryString = null
    Fragment = null
    VersionMajor = 1
    VersionMinor = 0
    Headers = dict [( "Foo", "Bar" ); ( "Connection", "keep-alive" ); ( "Content-Length", "15" )]
    OnHeadersEndCalled = false
    Body = Encoding.UTF8.GetBytes("helloworldhello")
    ShouldKeepAlive = true }
|]
