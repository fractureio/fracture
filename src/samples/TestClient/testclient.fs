open System
open Fracture

let quoteSize = 512
let testMessage = Array.init<byte> 512 (fun _ -> 1uy)
let testMessage2 = Array.init<byte> 1 (fun _ -> 1uy)
//type System.Net.Sockets.TcpClient with
//    member client.AsyncConnect(server, port, clientIndex) = 
//        Async.FromBeginEnd(server, port,(client.BeginConnect : IPAddress * int * _ * _ -> _), client.EndConnect)
//
//let clientRequestQuoteStream (clientIndex, server, port:int) =
//    async { 
//        let client = new System.Net.Sockets.TcpClient()
//        do!  client.AsyncConnect(server,port, clientIndex)
//        let stream = client.GetStream()
//        while true do
//            do! stream.AsyncWrite(testMessage, 0, testMessage.Length)
//            //let! x = stream.AsyncRead(1)
//            //x |> printfn "Data: %A"
//            do! Async.Sleep(1000)
//    }
//
//let myLock = new obj()
//
//let clientAsync clientIndex = 
//    async { 
//        do! Async.Sleep(clientIndex*1000)
//        if clientIndex % 10 = 0 then
//            lock myLock (fun() -> printfn "%d clients..." clientIndex)       
//        try 
//            do! clientRequestQuoteStream (clientIndex, IPAddress.Loopback, 10003)
//        with e -> 
//            printfn "CLIENT %d ERROR: %A" clientIndex e
//            //raise e
//    }
//   
//Async.Parallel [ for i in 1 .. 1 -> clientAsync i ] 
//    |> Async.Ignore 
//    |> Async.Start
//System.Console.ReadKey() |> ignore

let client = new TcpClient()
client.Sent |> Observable.add (fun x -> printfn  "**Sent: %A " (fst x).Length )
client.Received |> Observable.add (fun x -> do printfn "%A EndPoint: %A bytes received: %i" DateTime.Now.TimeOfDay (snd x) (fst x).Length )
client.Connected |> Observable.add (fun x -> 
    do printfn "%A Connected" DateTime.Now.TimeOfDay
    client.Send(testMessage))
client.Disconnected |> Observable.add (fun x -> printfn "%A Endpoint %A: Disconnected" DateTime.Now.TimeOfDay x)

client.Start(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 10003))

System.Console.ReadKey() |> ignore

