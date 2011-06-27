open System
open System.Net
open Fracture

let quoteSize = 512
let testMessage = Array.init<byte> 512 (fun _ -> 1uy)
let testMessage2 = Array.init<byte> 1 (fun _ -> 1uy)

//Async.Parallel [ for i in 1 .. 1 -> clientAsync i ] 
//    |> Async.Ignore 
//    |> Async.Start
//System.Console.ReadKey() |> ignore

let startclient i = 
    async{
    do! Async.Sleep(i*1000)
    let client = new TcpClient()
    client.Sent |> Observable.add (fun x -> printfn  "**Sent: %A " (fst x).Length )

    client.Received |> Observable.add (fun x -> do printfn "%A EndPoint: %A bytes received: %i" DateTime.Now.TimeOfDay (snd x) (fst x).Length )

    client.Connected 
    |> Observable.add (fun x -> 
        do printfn "%A Connected" DateTime.Now.TimeOfDay
        let sendloop = async{   while true do
                                    do! Async.Sleep(1000)
                                    client.Send(testMessage) }
        Async.Start sendloop )

    client.Disconnected |> Observable.add (fun x -> printfn "%A Endpoint %A: Disconnected" DateTime.Now.TimeOfDay x)
    client.Start(new IPEndPoint(IPAddress.Loopback, 10003))
    }

Async.Parallel [ for i in 1 .. 10 -> startclient (i) ] 
    |> Async.Ignore 
    |> Async.Start

System.Console.ReadKey() |> ignore