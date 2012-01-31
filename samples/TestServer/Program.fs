open System
open System.Collections.Generic
open System.Diagnostics
open Fracture
open Fracture.Http.Core

let debug (x:UnhandledExceptionEventArgs) =
    Console.WriteLine(sprintf "%A" (x.ExceptionObject :?> Exception))
    Console.ReadLine() |> ignore

System.AppDomain.CurrentDomain.UnhandledException |> Observable.add debug
let shortdate = DateTime.UtcNow.ToShortDateString
open Fracture.HttpServer

let data = "HTTP/1.0 200 OK\r\nContent-Type: text/plain\r\nConnection: Keep-Alive\r\nContent-Length: 16\r\nServer: Fracture\r\n\r\nHello world.\r\n\r\n"B

let server = new HttpServer( fun (req, res) -> 
    let result = res data
    () )

server.Start(6667)
printfn "Http Server started"
Console.ReadKey() |> ignore
