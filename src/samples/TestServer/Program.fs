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
let shortdate = DateTime.UtcNow.ToShortDateString
open Fracture.HttpServer

let server = HttpServer((fun(headers, close, svr, sd) -> 
    let response = sprintf "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 20\r\nConnection: close\r\nServer: Fracture\r\nDate: %s\r\n\r\nHello world.\r\nHello." (shortdate())
    do svr.Send(sd.RemoteEndPoint, response, true)), body = fun(body, svr, sd) -> 
        Debug.WriteLine( sprintf "%s" (Text.Encoding.ASCII.GetString(body.Array)) ) )

server.Start(6667)
printfn "Http Server started"
Console.ReadKey() |> ignore
