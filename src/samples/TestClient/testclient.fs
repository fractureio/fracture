open System
open System.Net
open Fracture

let quoteSize = 8000
let generateCircularSeq s = 
    let rec next() =  seq {
        for element in s do
            yield element
            yield! next() }
    next()

//let testMessage = seq{0uy..255uy} 
//                  |> generateCircularSeq 
//                  |> Seq.take quoteSize 
//                  |> Seq.toArray
let testMessage = Array.init quoteSize (fun x -> 0uy)

let startClient(port, i) = async {
    do! Async.Sleep(i*50)
    Console.WriteLine(sprintf "Client %d" i )
    let client = new TcpClient()
    client.Sent |> Observable.add (fun x -> Console.WriteLine( sprintf  "Sent: %A bytes" (fst x).Length) )

    client.Received
    |> Observable.add (fun x ->
        let res = sprintf "%A %A received: %i bytes" DateTime.Now.TimeOfDay (snd x) (fst x).Length 
        Console.WriteLine(res ))

    client.Connected 
    |> Observable.add (fun x -> 
        do Console.WriteLine(sprintf "%A Connected on %A" DateTime.Now.TimeOfDay x)
        let sendloop = async {
            while true do
                do! Async.Sleep(1000)
                client.Send(testMessage) }
        Async.Start sendloop)

    client.Disconnected |> Observable.add (fun x -> Console.WriteLine(sprintf "%A Endpoint %A: Disconnected" DateTime.Now.TimeOfDay x))
    client.Start(new IPEndPoint(IPAddress.Loopback, port)) }

Async.Parallel [ for i in 1 .. 1250 -> startClient (6667, i) ] 
    |> Async.Ignore 
    |> Async.Start

System.Console.ReadKey() |> ignore
