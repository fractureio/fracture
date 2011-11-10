namespace Fracture

type RemoteAgent(port, host) =
    let received(x, y, z) =
        ()
    let server = TcpServer.Create(received) 

    member this.Start(address, port) = server.Listen(address, port)
    member this.Stop = server.Dispose()