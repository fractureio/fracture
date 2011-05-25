module fracture
open System
open System.Threading
open System.Reflection
    
[<Interface>]
type IPipeletInput<'a> =
    abstract Post: 'a -> unit

type Message<'a> =
| Payload of 'a

type pipelet<'a,'b>(name:string, transform, router: seq<IPipeletInput<'b>> * 'b -> seq<IPipeletInput<'b>>, maxcount, maxwait:int)= 
        
    let routes = ref List.empty<IPipeletInput<'b>>
    let ss  = new SemaphoreSlim(maxcount, maxcount);
        
    let dorouting result routes=
            do result |> Seq.iter (fun msg -> router(routes, msg) |> Seq.iter (fun stage -> stage.Post(msg)))

    let mailbox = MailboxProcessor.Start(fun inbox ->
        let rec loop() = async {
            let! msg = inbox.Receive()
            ss.Release() |> ignore
            try
                match !routes with
                | [] ->()
                | _ as routes -> 
                    msg |> transform |> dorouting <| routes
                return! loop()
            with //force loop resume on error
            | ex -> 
                Console.WriteLine(sprintf "%A Error: %A" DateTime.Now.TimeOfDay ex.Message )
                return! loop()
            }
        loop())

    interface IPipeletInput<'a> with
        member this.Post payload =
            if ss.Wait(maxwait) then
                mailbox.Post(payload)
            else ( Console.WriteLine(sprintf "%A Overflow: %A" DateTime.Now.TimeOfDay payload ))  //overflow

    member this.Name with get() = name

    member this.Attach(stage) =
        let current = !routes
        routes := stage :: current
        
    member this.Detach (stage) =
        let current = !routes
        routes := List.filter (fun el -> el <> stage) current

let inline (<--) (m:pipelet<_,_>) msg = (m :> IPipeletInput<_>).Post(msg)
let inline (-->) msg (m:pipelet<_,_>)= (m :> IPipeletInput<_>).Post(msg)