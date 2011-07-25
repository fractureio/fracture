open System
open System.Net
open Fracture
open Fracture.Common
open HttpMachine
open System.Collections.Generic
open System.Diagnostics

let debug (x:UnhandledExceptionEventArgs) =
    Console.WriteLine(sprintf "%A" (x.ExceptionObject :?> Exception))
    Console.ReadLine() |> ignore

System.AppDomain.CurrentDomain.UnhandledException |> Observable.add debug

//try
//    use subscription = TcpServer.Create(fun (data,svr,sd) -> 
//        Console.WriteLine(System.Text.Encoding.ASCII.GetString(data))
//        let header = sprintf "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 20\r\nConnection: close\r\nServer: Fracture\r\nDate: %s\r\n\r\n" (DateTime.UtcNow.ToShortDateString())
//        let body = "Hello world.\r\nHello."
//        let encoded = System.Text.Encoding.ASCII.GetBytes(header + body)
//        do svr.Send(sd.RemoteEndPoint, encoded)).Listen(port = 6667)
//    "Server Running, press a key to exit." |> printfn "%s"
//    Console.ReadKey() |> ignore
//    subscription.Dispose()
//with
//| e ->
//    printfn "%s" e.Message
//    Console.ReadKey() |> ignore

//        Console.WriteLine(System.Text.Encoding.ASCII.GetString(data))
//        let header = sprintf "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 20\r\nConnection: close\r\nServer: Fracture\r\nDate: %s\r\n\r\n" (DateTime.UtcNow.ToShortDateString())
//        let body = "Hello world.\r\nHello."
//        let encoded = System.Text.Encoding.ASCII.GetBytes(header + body)
//        do svr.Send(sd.RemoteEndPoint, encoded)

open Fracture.HttpServer

let processheaders(headers:HttpRequestHeaders, close, svr:TcpServer, sd) =
     //Console.WriteLine(System.Text.Encoding.ASCII.GetString(data))
    let header = sprintf "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 20\r\nConnection: close\r\nServer: Fracture\r\nDate: %s\r\n\r\n" (DateTime.UtcNow.ToShortDateString())
    let body = "Hello world.\r\nHello."
    let encoded = System.Text.Encoding.ASCII.GetBytes(header + body)
    do svr.Send(sd.RemoteEndPoint, encoded, true)

let processbody(body:ArraySegment<_>, svr, sd) =
    Console.WriteLine( sprintf "%s" (Text.Encoding.ASCII.GetString(body.Array)) )

let server = HttpServer((fun(headers, close, svr, sd) -> 
    let header = sprintf "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 20\r\nConnection: close\r\nServer: Fracture\r\nDate: %s\r\n\r\n" (DateTime.UtcNow.ToShortDateString())
    let body = "Hello world.\r\nHello."
    let encoded = System.Text.Encoding.ASCII.GetBytes(header + body)
    do svr.Send(sd.RemoteEndPoint, encoded, true)), body = fun(body, svr, sd) -> 
        Console.WriteLine( sprintf "%s" (Text.Encoding.ASCII.GetString(body.Array)) ) )

server.Listen(6667)
printfn "Http Server started"
Console.ReadKey() |> ignore
