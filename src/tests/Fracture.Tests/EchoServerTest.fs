module Fracture.Tests.EchoServerTest

open System
open System.Net
open Fracture
open NUnit.Framework
open FsUnit

let testEndPoint = IPEndPoint(IPAddress.Parse("127.0.0.1"), 81)
let isListeningTo endPoint = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners() |> Seq.exists (fun ip -> ip = endPoint)

let shouldBeListening() = isListeningTo testEndPoint |> should be True
let shouldNotBeListening() = isListeningTo testEndPoint |> should be False

let makeEchoServer(poolSize:int, perSocketBuffer:int, backlog:int) =
    let received (data:byte[], client:IPEndPoint, echo:byte[]->int->unit) =
        echo data 1
        
    let connected (client:IPEndPoint) = ()
    let disconnected (client:IPEndPoint) = ()
    let sent (data:byte[], client:IPEndPoint) = ()

    let server = new TcpServer(poolSize, perSocketBuffer, backlog, received, connected, disconnected, sent)

    server

let makeTestEchoServer() = makeEchoServer(4,1,2)

let listen (server:TcpServer) = server.Listen(testEndPoint.Address.ToString(), testEndPoint.Port)

let makeEchoClient endPoint poolSize size =
    let client = new TcpClient(endPoint, poolSize, size)
    client


[<TestCase(4,1,2)>]
let ``server starts and stop cleanly even if no one connects``(poolSize:int, perSocketBuffer:int, backlog:int) = 
    use server = makeEchoServer(poolSize, perSocketBuffer, backlog)
    shouldNotBeListening()
    ()

[<TestCase(1,1)>]
let ``client starts and stops cleanly even if it does not connect``(poolSize:int, size:int) =
    use client = makeEchoClient testEndPoint poolSize size
    ()

[<Test>]
let ``server listens when started and stops listening when stopped``() =
    let startAndStop() =
        shouldNotBeListening()
        use server = makeTestEchoServer()
        server |> listen
        shouldBeListening()
    startAndStop()
    shouldNotBeListening()

[<Test>]
let ``server cannot be started twice``() =
    shouldNotBeListening()
    use server = makeTestEchoServer()
    server |> listen
    shouldBeListening()
    (fun () -> server |> listen) |> should throw typeof<InvalidOperationException>
    
