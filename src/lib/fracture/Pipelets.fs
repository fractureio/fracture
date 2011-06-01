module Fracture.Pipelets
open System
open System.Threading
open System.Reflection
    
[<Interface>]
type IPipeletInput<'a> =
    abstract Post: 'a -> unit

type Message<'a> = Payload of 'a

type Pipelet<'a,'b>(name:string, transform, router:seq<IPipeletInput<'b>> * 'b -> seq<IPipeletInput<'b>>, maxcount, maxwait:int) = 

    let disposed = ref false
    let routes = ref List.empty<IPipeletInput<'b>>
    let ss = new SemaphoreSlim(maxcount, maxcount)

    let dispose disposing =
        if not !disposed then
            if disposing then ss.Dispose()
            disposed := true
        
    let dorouting result routes=
        do result |> Seq.iter (fun msg -> router(routes, msg) |> Seq.iter (fun stage -> stage.Post(msg)))

    let mailbox = MailboxProcessor.Start(fun inbox ->
        let rec loop() = async {
            let! msg = inbox.Receive()
            ss.Release() |> ignore
            try
                match !routes with
                | [] ->()
                | _ as routes -> msg |> transform |> dorouting <| routes
                return! loop()
            with //force loop resume on error
            | ex -> 
                // TODO: Allow exceptional occurrences to be returned via other mechanisms than just printing to the console.
                printf "%A Error: %A" DateTime.Now.TimeOfDay ex.Message
                return! loop()
        }
        loop())

    member this.Name with get() = name

    member this.Attach(stage) =
        let current = !routes
        routes := stage :: current
        
    member this.Detach (stage) =
        let current = !routes
        routes := List.filter (fun el -> el <> stage) current

    interface IPipeletInput<'a> with
        member this.Post payload =
            if ss.Wait(maxwait) then
                mailbox.Post(payload)
            // TODO: Allow exceptional occurrences to be returned via other mechanisms than just printing to the console.
            else printf "%A Overflow: %A" DateTime.Now.TimeOfDay payload  //overflow

    interface IDisposable with
        member this.Dispose() =
            dispose true
            GC.SuppressFinalize(this)

let inline (<--) (m:Pipelet<_,_>) msg = (m :> IPipeletInput<_>).Post(msg)
let inline (-->) msg (m:Pipelet<_,_>)= (m :> IPipeletInput<_>).Post(msg)

let inline (++>) (a:Pipelet<_,_>) b = a.Attach(b);b
let inline (-+>) (a:Pipelet<_,_>) b = a.Detach(b);b