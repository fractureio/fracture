//----------------------------------------------------------------------------
//
// Copyright (c) 2011-2012 Dave Thomas (@7sharp9) Ryan Riley (@panesofglass)
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//----------------------------------------------------------------------------
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

let makeEchoClient endPoint poolSize size =
    let client = new TcpClient(endPoint, poolSize, size)
    client


[<TestCase(4,1,2)>]
let ``server starts and stop cleanly even if no one connects``(poolSize:int, perSocketBuffer:int, backlog:int) = 
    use server = makeEchoServer(poolSize, perSocketBuffer, backlog)
    ()

[<TestCase(1,1)>]
let ``client starts and stops cleanly even if it does not connect``(poolSize:int, size:int) =
    use client = makeEchoClient testEndPoint poolSize size
    ()

<<<<<<< HEAD
=======
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
>>>>>>> 7eaa906... transparently enforce minimum pool sizes: two per pool, as a new saea is pulled from the pool while the other is still being used, even to accept one connection or send one message.
