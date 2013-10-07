namespace Fracture

type RemoteAgent(port, host) =
    let server = TcpServer.Create()
    member this.Start(address, port) = server.Listen(address, port)
    member this.Stop = server.Dispose()