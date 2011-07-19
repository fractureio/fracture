module Fracture.Tests.EchoServerTest

open System
open System.Net
open System.Net.Sockets
open Fracture
open NUnit.Framework
open FsUnit

let testEndPoint = IPEndPoint(IPAddress.Parse("127.0.0.1"), 81)
let isListeningTo endPoint = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners() |> Seq.exists (fun ip -> ip = endPoint)

let shouldBeListening() = isListeningTo testEndPoint |> should be True
let shouldNotBeListening() = isListeningTo testEndPoint |> should be False

let makeEchoServer(poolSize:int, perSocketBuffer:int, backlog:int) =
    let received (data:byte[], client:IPEndPoint, echo:byte[]->unit) =
        echo data
        
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
    
[<Test>]
let ``echo server will echo data from naive socket call``([<Values(4,10,20)>]poolSize:int, [<Values(1,2,10,40)>]perSocketBuffer:int, [<Values(2)>]backlog:int, [<Values(1,2,4,8,32,64)>]messageLength:int) = 
    // note we'll test the TcpServer using a simple naked socket to keep one foot on the ground.
    // the client will disconnect after sending its message
    use server = makeEchoServer(poolSize, perSocketBuffer, backlog)
    server |> listen
    use socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    socket.Connect(testEndPoint)
    use stream = new NetworkStream(socket, true)
    let message:byte[] = [| for i in 0 .. messageLength-1 -> byte(i % 256) |]
    stream.Write(message, 0, message.Length)
    let buffer:byte[] = Array.zeroCreate message.Length
    let rec read i =
        let bytes = stream.Read(buffer, i, message.Length - i)
        if bytes + i < message.Length then read (i + bytes)
    read 0
    buffer |> should equal message
