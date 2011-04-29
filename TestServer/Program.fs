open System
open System.Net
open flack

try
    let display a b =
        let xx = a |> printfn "%s: %A"
        b |> xx

    use server = new TcpListener(50, 512, 10003, 1000)

    server.Sent |> Observable.add (fun x -> display "Sent" x)

    server.Received |> Observable.add (fun x -> do printfn "EndPoint: %A bytes received: %i" (snd x) (fst x).Length )

    server.Connected |> Observable.add (fun x -> printfn "endpoint %A: Connected" x)

    server.Disconnected |> Observable.add (fun x -> printfn "endpoint %A: Disconnected" x)

    server.Start ()
    "Server Running, press a key to exit." |> printfn "%s"
    Console.ReadKey() |> ignore
with
|   e ->
    printfn "%s" e.Message
    Console.ReadKey() |> ignore