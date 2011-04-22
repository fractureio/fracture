open System
open System.Net
open flack

try
    let display a b = 
        let xx = a |> printfn "%s: %A"
        b |> xx

    let displaySend b  = display  b

    let displayReceive b = display "Receive: " b

    let endpointConnected (endpoint: IPEndPoint)=
        printfn "endpoint %A: Connected" endpoint

    let endpointDisonnected (endpoint: IPEndPoint)=
        printfn "endpoint %A: Disconnected" endpoint

    
    use server = new TcpListener(10, 2, 2, 128, 10003, 1000, displaySend, displayReceive, Some endpointConnected, endpointDisonnected )
    server.start ()
    "Server Running, press a key to exit." |> printfn "%s"
    Console.ReadKey() |> ignore
with
|   e -> 
    printfn "%s" e.Message
    Console.ReadKey() |> ignore