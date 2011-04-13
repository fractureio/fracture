open System
open flack

try
    use server = new TcpListener(10, 1, 1, 128, 10003, 1000)
    server.start ()
with
|   e -> 
    printfn "%s" e.Message
    Console.ReadKey() |> ignore