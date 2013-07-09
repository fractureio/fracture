namespace Fracture

type RemoteAgent(port, host) =
    let server = new TcpServer() 
    member this.Start(address, port) = server.Listen(address, port)
    member this.Stop = server.Dispose()