module Fracture.Pipelets

open System
open System.Threading
open System.Reflection
    
[<Interface>]
type IPipeletInput<'a> =
    /// Posts a message to the pipelet input.
    abstract Post: 'a -> unit

/// A wrapper for pipeline payloads.
type Message<'a, 'b> =
    | Payload of 'a
    | Attach of 'b IPipeletInput
    | Detach of 'b IPipeletInput

/// A pipelet is a named, write-only agent that processes incoming messages
/// then publishes the results to the next pipeline stage(s).
type Pipelet<'a, 'b>(name:string, transform:'a -> 'b seq, router:'b seq -> 'b IPipeletInput seq -> unit, maxcount, maxwait:int) = 

    let disposed = ref false
    let ss = new SemaphoreSlim(maxcount, maxcount)

    let dispose disposing =
        if not !disposed then
            if disposing then ss.Dispose()
            disposed := true
        
    let mailbox = MailboxProcessor.Start(fun inbox ->
        let rec loop routes = async {
            let! msg = inbox.Receive()
            match msg with
            | Payload(data) ->
                ss.Release() |> ignore
                try
                    data |> transform |> router <| routes
                    return! loop routes
                with //force loop resume on error
                | ex -> 
                    // TODO: Allow exceptional occurrences to be returned via other mechanisms than just printing to the console.
                    printf "%A Error: %A" DateTime.Now.TimeOfDay ex.Message
                    return! loop routes
            | Attach(stage) -> return! loop (stage::routes)
            | Detach(stage) -> return! loop (List.filter (fun x -> x <> stage) routes)
        }
        loop [])

    let post data =
        if ss.Wait(maxwait) then
            mailbox.Post(Payload data)
        // TODO: Allow exceptional occurrences to be returned via other mechanisms than just printing to the console.
        else printf "%A Overflow: %A" DateTime.Now.TimeOfDay data //overflow

    /// Gets the name of the pipelet.
    member this.Name with get() = name

    /// Attaches a subsequent stage.
    member this.Attach(stage) = mailbox.Post(Attach stage)
        
    /// Detaches a subsequent stage.
    member this.Detach (stage) = mailbox.Post(Detach stage)

    /// Posts a message to the pipelet agent.
    member this.Post(data) = post data

    interface IPipeletInput<'a> with
        /// Posts a message to the pipelet input.
        member this.Post(data) = post data

    interface IDisposable with
        /// Disposes the pipelet, along with the encapsulated SemaphoreSlim.
        member this.Dispose() =
            dispose true
            GC.SuppressFinalize(this)

let inline (<--) (m:Pipelet<_,_>) msg = (m :> IPipeletInput<_>).Post(msg)
let inline (-->) msg (m:Pipelet<_,_>)= (m :> IPipeletInput<_>).Post(msg)

let inline (++>) (a:Pipelet<_,_>) b = a.Attach(b);b
let inline (-+>) (a:Pipelet<_,_>) b = a.Detach(b);b

/// Picks a circular sequence of routes that repeats i.e. A,B,C,A,B,C etc
let roundRobin<'a> =
    let makeseqskipper =
        let tmnt = ref 0
        let tick(seq) =
            tmnt := (!tmnt + 1) % (Seq.length seq)
            Seq.take 1 <| Seq.skip !tmnt seq 
        tick

    let createroundrobin messages (routes) =
        if routes |> Seq.isEmpty then ()
        else 
            let route =  makeseqskipper routes
            messages |> Seq.iter (fun msg -> do route |> Seq.iter (fun (s:'a IPipeletInput) -> s.Post msg) )
    createroundrobin

/// Simply picks the first route
let basicRouter messages (routes:'a IPipeletInput seq) =
    if routes |> Seq.isEmpty then ()
    else let route = routes |> Seq.head in messages |> Seq.iter (fun msg -> do route.Post msg)

//sends the message to all attached routes
let multicastRouter messages (routes:'a IPipeletInput seq) =
    if routes |> Seq.isEmpty then ()
    else messages |> Seq.iter (fun msg -> do routes |> Seq.iter (fun (s:'a IPipeletInput) -> s.Post msg) )
    