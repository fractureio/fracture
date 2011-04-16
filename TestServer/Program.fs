open System
open flack

try
    let display a b = 
        let xx = a |> printfn "%s: %A"
        b |> xx

    let displaySend b = display "Sent: " b
    let displayReceive b = display "Receive: " b
     
    use server = new TcpListener(10, 1, 1, 128, 10003, 1000, displaySend , displayReceive )
    server.start ()
with
|   e -> 
    printfn "%s" e.Message
    Console.ReadKey() |> ignore