module Fracture.HttpMachine

open System
open System.Net
open Fracture
open Fracture.Common
open HttpMachine
open System.Collections.Generic
open System.Diagnostics

type HttpRequestHeaders = {Method:string; Uri:string; Version:Version; Headers:IDictionary<string, string>;}
type HttpRequest = {RequestHeaders:HttpRequestHeaders; Body:ArraySegment<byte>}

    ///Note: Direct port of Kayaks Parser delegate, temp solution until Ryans parser arrives
    type ParserDelegate(requestBegan, requestBody, requestEnded)as p =
        [<DefaultValue>] val mutable method' : string
        [<DefaultValue>] val mutable requestUri: string
        [<DefaultValue>] val mutable fragment : string
        [<DefaultValue>] val mutable queryString : string
        [<DefaultValue>] val mutable headerName : string
        [<DefaultValue>] val mutable headerValue : string
        [<DefaultValue>] val mutable fullRequest:HttpRequest 
        [<DefaultValue>] val mutable requestHeaders:HttpRequestHeaders 
        [<DefaultValue>] val mutable body:ArraySegment<byte>
        let mutable headers = new Dictionary<string,string>()

        let commitHeader() = 
            headers.Add(p.headerName,p.headerValue)
            p.headerName <- null
            p.headerValue <- null
            ()

        interface IHttpParserHandler with
            member this.OnMessageBegin(parser:HttpParser) =
                this.method' <- this.requestUri 
                this.fragment <- null
                this.queryString <- null
                this.headerName <- null
                this.headerValue <- null
                headers.Clear()

            member this.OnMethod( parser, m) =
                this.method' <- m

            member this.OnRequestUri( parser:HttpParser,  requestUri:string) =
                this.requestUri <- requestUri

            member this.OnFragment( parser:HttpParser,  fragment:string) =
                this.fragment <- fragment

            member this.OnQueryString( parser:HttpParser,  queryString:string) =
                this.queryString <- queryString

            member this.OnHeaderName( parser:HttpParser,  name:string) = 
                if not (String.IsNullOrEmpty(this.headerValue) ) then
                    commitHeader()

                this.headerName <- name

            member this.OnHeaderValue( parser:HttpParser,  value:string) = 
                if ( String.IsNullOrEmpty(this.headerName)) then
                    failwith "Got header value without name."

                this.headerValue <- value

            member this.OnHeadersEnd( parser:HttpParser) = 
                Debug.WriteLine("OnHeadersEnd")

                if not (String.IsNullOrEmpty(this.headerValue)) then
                    commitHeader();

                p.requestHeaders <- { // TODO path, query, fragment?
                                Method = this.method';
                                Uri = this.requestUri;
                                Headers = headers;
                                Version = new Version(parser.MajorVersion, parser.MinorVersion)}

                requestBegan(p.requestHeaders, parser.ShouldKeepAlive);

            member this.OnBody(pars:HttpParser, data: ArraySegment<byte>) =
                Debug.WriteLine("OnBody")
                // XXX can we defer this check to the parser?
                if (data.Count > 0) then
                    p.body <- data
                    requestBody(p.body)

            member this.OnMessageEnd( parser:HttpParser) =
                Debug.WriteLine("OnMessageEnd")
                requestEnded({RequestHeaders= p.requestHeaders; Body= p.body})
