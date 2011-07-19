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
        received = (fun (data,ep,send) -> (send data))
        (*connected = fun(ep) -> ()*)
    ).Listen(port = 6667)

    "Server Running, press a key to exit." |> printfn "%s"
    Console.ReadKey() |> ignore
with
| e ->
    printfn "%s" e.Message
    Console.ReadKey() |> ignore
