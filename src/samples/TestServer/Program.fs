open System
open System.Net
open Fracture

let debug (x:UnhandledExceptionEventArgs) =
    Console.WriteLine(sprintf "%A" (x.ExceptionObject :?> Exception))
    Console.ReadLine() |> ignore

System.AppDomain.CurrentDomain.UnhandledException |> Observable.add debug

try
    TcpServer.Create(
        disconnected = (fun ep -> Console.WriteLine(sprintf "%A %A: Disconnected" DateTime.Now.TimeOfDay ep)), 
        sent = (fun (received,ep) -> Console.WriteLine( sprintf  "%A Sent: %A " DateTime.Now.TimeOfDay received.Length )),
        received = (fun (a,b,c) -> 
            (Console.WriteLine(System.Text.Encoding.ASCII.GetString(a))
             let header = sprintf "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 20\r\nServer: Fracture\r\nDate: %s\r\n\r\n" (DateTime.UtcNow.ToString())
                            
             let body = "Hello world.\r\nHello."

             let encoded = System.Text.Encoding.ASCII.GetBytes(header + body)
             c encoded encoded.Length true))
        (*connected = fun(ep) -> ()*)
    ).Listen(port = 6667)

    "Server Running, press a key to exit." |> printfn "%s"
    Console.ReadKey() |> ignore
with
| e ->
    printfn "%s" e.Message
    Console.ReadKey() |> ignore
