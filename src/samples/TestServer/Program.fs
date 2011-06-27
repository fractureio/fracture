open System
open System.Net
open Fracture

System.AppDomain.CurrentDomain.UnhandledException |> Observable.add (fun x -> 
    printfn "%A" (x.ExceptionObject :?> Exception); Console.ReadKey() |> ignore)
try

    use server = new TcpListener(new IPEndPoint(IPAddress.Loopback, 10003), 50, 512, 100)

    server.Sent |> Observable.add (fun x -> printfn  "**Sent: %A " (fst x).Length )

    server.Received |> Observable.add (fun x -> do printfn "%A EndPoint: %A bytes received: %i" DateTime.Now.TimeOfDay (snd x) (fst x).Length )
    
    let testbuffer = [| 0uy .. 129uy |]

    let sendOnConnect x = 
        printfn "%A Endpoint: %A: Connected, sending %i test Bytes" DateTime.Now.TimeOfDay x testbuffer.Length
        server.Send(x, testbuffer)

    server.Connected |> Observable.add (fun x -> sendOnConnect x)

    server.Disconnected |> Observable.add (fun x -> printfn "%A Endpoint %A: Disconnected" DateTime.Now.TimeOfDay x)

    server.Start ()
    "Server Running, press a key to exit." |> printfn "%s"
    Console.ReadKey() |> ignore
with
|   e ->
    printfn "%s" e.Message
    Console.ReadKey() |> ignore