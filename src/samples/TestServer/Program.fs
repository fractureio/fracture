open System
open System.Net
open Fracture

let debug(x:UnhandledExceptionEventArgs)=
    Console.WriteLine(sprintf "%A" (x.ExceptionObject :?> Exception))
    Console.ReadLine() |> ignore

System.AppDomain.CurrentDomain.UnhandledException |> Observable.add debug

try
    let sent ((a:byte[]), b) = Console.WriteLine( sprintf  "**Sent: %A " a.Length )
    let disconnected(a:IPEndPoint) = Console.WriteLine(sprintf "%A Endpoint %A: Disconnected" DateTime.Now.TimeOfDay a)
    use server = new TcpServer(6000, 4096, 100, disconnected = disconnected, sent = sent )

//    server.Sent |> Observable.add (fun x -> Console.WriteLine( sprintf  "**Sent: %A " (fst x).Length ))
//    //server.Received |> Observable.add (fun x -> do Console.WriteLine(sprintf "%A EndPoint: %A bytes received: %i" DateTime.Now.TimeOfDay (snd x) (fst x).Length ))
//    //server.Connected |> Observable.add (fun x -> Console.WriteLine(sprintf "%A Endpoint: %A: Connected" DateTime.Now.TimeOfDay x))
//    server.Disconnected |> Observable.add (fun x -> Console.WriteLine(sprintf "%A Endpoint %A: Disconnected" DateTime.Now.TimeOfDay x))

    server.listen("127.0.0.1", 6667)

    Async.Start(let rec loop() = 
                    async{do! Async.Sleep 5000
                          Console.WriteLine(sprintf "Current connections: %d" !server.Connections)
                          return! loop()}
                loop())

    "Server Running, press a key to exit." |> printfn "%s"
    Console.ReadKey() |> ignore
with
|   e ->
    printfn "%s" e.Message
    Console.ReadKey() |> ignore