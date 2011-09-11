namespace Fracture

type RemoteAgent(port, host) =
    let received(x, y, z) =
        ()
    let server = TcpServer.Create(received) 

    member this.Start(address, port) = server.Start(address, port)
    member this.Stop = server.Stop()