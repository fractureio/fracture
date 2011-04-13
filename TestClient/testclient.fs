open System.Net
open System.Net.Sockets

let quoteSize = 128

type System.Net.Sockets.TcpClient with
    member client.AsyncConnect(server, port, clientIndex) = 
        Async.FromBeginEnd(server, port,(client.BeginConnect : IPAddress * int * _ * _ -> _), client.EndConnect)

let clientRequestQuoteStream (clientIndex, server, port:int) =
    async { 
        let client = new System.Net.Sockets.TcpClient()
        do!  client.AsyncConnect(server,port, clientIndex)
        let stream = client.GetStream()
        let! header = stream.AsyncRead 1 // read header
        while true do
            let! bytes = stream.AsyncRead quoteSize
            if Array.length bytes <> quoteSize then 
                printfn "client incorrect checksum" 
    }

let myLock = new obj()

let clientAsync clientIndex = 
    async { 
        do! Async.Sleep(clientIndex*15)
        if clientIndex % 10 = 0 then
            lock myLock (fun() -> printfn "%d clients..." clientIndex)       
        try 
            do! clientRequestQuoteStream (clientIndex, IPAddress.Loopback, 10003)
        with e -> 
            printfn "CLIENT %d ERROR: %A" clientIndex e
            //raise e
    }
   
Async.Parallel [ for i in 1 .. 50 -> clientAsync i ] 
    |> Async.Ignore 
    |> Async.Start
System.Console.ReadKey() |> ignore
