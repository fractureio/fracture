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
    let random = Random(poolSize*1000000 + perSocketBuffer*1000 + backlog)
    let randomOffset() = random.Next(10)
    let received (data:ArraySegment<byte>, client:IPEndPoint, echo:ArraySegment<byte>->unit) =
        if random.NextDouble() < 0.5 then
            let offset1 = randomOffset()
            let offset2 = randomOffset()
            let localBuffer = Array.zeroCreate (data.Count + offset1 + offset2)
            Array.blit (data.Array) (data.Offset) localBuffer offset1 data.Count
            echo(ArraySegment(localBuffer, offset1, data.Count))
        else
            echo data
        
    let connected (client:IPEndPoint) = ()
    let disconnected (client:IPEndPoint) = ()
    let sent (data:ArraySegment<byte>, client:IPEndPoint) = ()

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

/// Test the server from a naive blocking socket call to make sure it's doing what we think it should
/// we use buffer sizes, message sizes and chunks in different configurations to make sure everything
/// is buffering as it is supposed to. By creating and tearing down the server so many times we stress
/// the disposal APIs quite a bit.
[<Test>]
let ``echo server will echo data from naive socket call``
    ([<Values(1,4,10)>]        poolSize:int, //note 2 appears to be the minimum pool size with our implementation for a single client, so this is also testing that we adjust it properly
     [<Values(1,2,32,128)>]    perSocketBuffer:int, 
     [<Values(1,5)>]           backlog:int,  // 2 is the minimum size for this backlog too, so this is testing that we adjust it properly
     [<Values(1,2,3,17,64)>]   messageLength:int,
     [<Values(0,1,7)>]         chunkSize:int) = // 0 chunk size means send the whole thing
    shouldNotBeListening()
    let echoTest() =
        use server = makeEchoServer(poolSize, perSocketBuffer, backlog)
        server |> listen
        shouldBeListening()
        use socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        socket.Connect(testEndPoint)
        use stream = new NetworkStream(socket, true, WriteTimeout = 100, ReadTimeout = 100)
        let message:byte[] = [| for i in 0 .. messageLength-1 -> byte(i % 256) |]
        let rec write i n =
            stream.Write(message, i, min n (message.Length - i))
            if i + n < message.Length then write (i+n) n
        write 0 (if chunkSize = 0 then message.Length else chunkSize)
        let buffer:byte[] = Array.zeroCreate message.Length
        let rec read i =
            let bytes = stream.Read(buffer, i, message.Length - i)
            if bytes + i < message.Length then read (i + bytes)
        read 0
        buffer |> should equal message
    echoTest()
    shouldNotBeListening()
