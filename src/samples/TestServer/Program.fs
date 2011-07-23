open System
open System.Net
open Fracture
open HttpMachine
open System.Collections.Generic
open System.Diagnostics

let debug (x:UnhandledExceptionEventArgs) =
    Console.WriteLine(sprintf "%A" (x.ExceptionObject :?> Exception))
    Console.ReadLine() |> ignore

System.AppDomain.CurrentDomain.UnhandledException |> Observable.add debug

type HttpRequestHeaders = {Method:string; Uri:string; Version:Version; Headers:IDictionary<string, string>;}

//    interface IHighLevelParserDelegate
//    {
//        void OnRequestBegan(HttpRequestHeaders request, bool shouldKeepAlive);
//        void OnRequestBody(ArraySegment<byte> data);
//        void OnRequestEnded();
//    }

    type ParserDelegate(requestBegan, requestBody, requestEnded)as p =
        [<DefaultValue>] val mutable method' : string
        [<DefaultValue>] val mutable requestUri: string
        [<DefaultValue>] val mutable fragment : string
        [<DefaultValue>] val mutable queryString : string
        [<DefaultValue>] val mutable headerName : string
        [<DefaultValue>] val mutable headerValue : string
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

                let request = { // TODO path, query, fragment?
                                Method = this.method';
                                Uri = this.requestUri;
                                Headers = headers;
                                Version = new Version(parser.MajorVersion, parser.MinorVersion)}

                requestBegan(request, parser.ShouldKeepAlive);

            member this.OnBody(pars:HttpParser, data: ArraySegment<byte>) =
                Debug.WriteLine("OnBody")
                // XXX can we defer this check to the parser?
                if (data.Count > 0) then
                    requestBody(data)

            member this.OnMessageEnd( parser:HttpParser) =
                Debug.WriteLine("OnMessageEnd")
                requestEnded()

try
    use subscription = TcpServer.Create(fun (a,svr,sd) -> 
        Console.WriteLine(System.Text.Encoding.ASCII.GetString(a))
        let header = sprintf "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 20\r\nConnection: close\r\nServer: Fracture\r\nDate: %s\r\n\r\n" (DateTime.UtcNow.ToShortDateString())
        let body = "Hello world.\r\nHello."
        let encoded = System.Text.Encoding.ASCII.GetBytes(header + body)
        svr.Send(sd.RemoteEndPoint, encoded)).Listen(port = 6667)

    "Server Running, press a key to exit." |> printfn "%s"
    Console.ReadKey() |> ignore
    subscription.Dispose()
with
| e ->
    printfn "%s" e.Message
    Console.ReadKey() |> ignore
