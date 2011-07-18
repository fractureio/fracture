open System
open System.Net
open Fracture

let debug (x:UnhandledExceptionEventArgs) =
    Console.WriteLine(sprintf "%A" (x.ExceptionObject :?> Exception))
    Console.ReadLine() |> ignore

System.AppDomain.CurrentDomain.UnhandledException |> Observable.add debug

try
    use subscription = TcpServer.Create(fun (a,b,reply) -> 
        Console.WriteLine(System.Text.Encoding.ASCII.GetString(a))
        let header = sprintf "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 20\r\nConnection: close\r\nServer: Fracture\r\nDate: %s\r\n\r\n" (DateTime.UtcNow.ToShortDateString())
                        
        let body = "Hello world.\r\nHello."

        let encoded = System.Text.Encoding.ASCII.GetBytes(header + body)
        reply encoded encoded.Length true).Listen(port = 6667)

    "Server Running, press a key to exit." |> printfn "%s"
    Console.ReadKey() |> ignore
    subscription.Dispose()
with
| e ->
    printfn "%s" e.Message
    Console.ReadKey() |> ignore
