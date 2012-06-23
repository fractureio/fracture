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

/// A pipelet is a named, write-only agent that processes incoming messages then publishes the results to the next pipeline stage(s).
type Pipelet<'a, 'b>(name:string, transform:'a -> 'b seq, router:'b seq -> 'b IPipeletInput seq -> unit, maxcount, maxwait:int, ?overflow, ?errors) = 

    let mutable disposed = false
    let ss = new SemaphoreSlim(maxcount, maxcount)
    let overflow = defaultArg overflow (fun x -> printf "%A Overflow: %A" DateTime.Now.TimeOfDay x)
    let errors = defaultArg errors (fun (e:Exception) -> printf "%A Processing error: %A" DateTime.Now.TimeOfDay e.Message)

    let dispose disposing =
        if not disposed then
            if disposing then ss.Dispose()
            disposed <- true

//    let mailbox = MailboxProcessor.Start(fun inbox ->
//        let rec loop routes = async {
//            let! msg = inbox.Receive()
//            match msg with
//            | Payload(data) ->
//                ss.Release() |> ignore
//                try
//                    data |> transform |> router <| routes
//                with //force loop resume on error
//                | ex -> errors ex
//                return! loop routes
//            | Attach(stage) -> return! loop (stage::routes)
//            | Detach(stage) -> return! loop (List.filter (fun x -> x <> stage) routes)
//        }
//        loop [])
          
    let computeAndRoute data routes = 
        try
            data |> transform |> router <| routes
            Choice1Of2()
        with 
        | ex -> Choice2Of2 ex

    let mailbox = MailboxProcessor.Start(fun inbox ->
        let rec loop routes = async {
            let! msg = inbox.Receive()
            match msg with
            | Payload(data) ->
                ss.Release() |> ignore
                match computeAndRoute data routes with
                | Choice1Of2 _ -> ()
                | Choice2Of2 exn -> errors exn
                return! loop routes
            | Attach(stage) -> return! loop (stage::routes)
            | Detach(stage) -> return! loop (List.filter (fun x -> x <> stage) routes)
        }
        loop [])

//    let mailbox = MailboxProcessor.Start(fun inbox ->
//      let rec loop routes = async {
//        let! msg = inbox.Receive()
//        match msg with
//        | Payload(data) ->
//          ss.Release() |> ignore
//          let result = async{data |> transform |> router <| routes} |> Async.Catch |> Async.RunSynchronously
//          match result with
//          | Choice1Of2() -> ()
//          | Choice2Of2 exn -> errors exn
//          return! loop routes
//        | Attach(stage) -> return! loop (stage::routes)
//        | Detach(stage) -> return! loop (List.filter (fun x -> x <> stage) routes)
//      }
//      loop [])

    interface IPipeletInput<'a> with
        /// Posts a message to the pipelet input.
        member this.Post(data) = 
            if ss.Wait(maxwait) then
                mailbox.Post(Payload data)
            else overflow data

    interface IDisposable with
        /// Disposes the pipelet, along with the encapsulated SemaphoreSlim.
        member this.Dispose() =
            dispose true
            GC.SuppressFinalize(this)

    /// Gets the name of the pipelet.
    member this.Name with get() = name

    /// Attaches a subsequent stage.
    member this.Attach(stage) = mailbox.Post(Attach stage)
        
    /// Detaches a subsequent stage.
    member this.Detach (stage) = mailbox.Post(Detach stage)

    /// Posts a message to the pipelet agent.
    member this.Post(data) = (this :> IPipeletInput<'a>).Post(data)

let inline (<--) (m:Pipelet<_,_>) msg = (m :> IPipeletInput<_>).Post(msg)
let inline (-->) msg (m:Pipelet<_,_>)= (m :> IPipeletInput<_>).Post(msg)

let inline (++>) (a:Pipelet<_,_>) b = a.Attach(b);b
let inline (-+>) (a:Pipelet<_,_>) b = a.Detach(b);b

let inline manyToOne many one = 
    for p in many do
        p ++> one |> ignore 

let inline oneToMany one many =
    for p in many do
        one ++> p |> ignore

module Routers =
    /// Picks a circular sequence of routes that repeats i.e. A,B,C,A,B,C etc
    let roundRobin<'a> =
        let makeSeqSkipper =
            let tmnt = ref 0
            let tick(seq) =
                tmnt := (!tmnt + 1) % (Seq.length seq)
                Seq.take 1 <| Seq.skip !tmnt seq 
            tick

        let createRoundRobin messages (routes) =
            if routes |> Seq.isEmpty then ()
            else 
                let route =  makeSeqSkipper routes
                messages |> Seq.iter (fun msg -> route |> Seq.iter (fun (s:'a IPipeletInput) -> s.Post msg) )
        createRoundRobin

    /// Simply picks the first route
    let basicRouter messages (routes:'a IPipeletInput seq) =
        if routes |> Seq.isEmpty then ()
        else let route = routes |> Seq.head in messages |> Seq.iter (fun msg -> route.Post msg)

    //sends the message to all attached routes
    let multicastRouter messages (routes:'a IPipeletInput seq) =
        if routes |> Seq.isEmpty then ()
        else messages |> Seq.iter (fun msg -> routes |> Seq.iter (fun (s:'a IPipeletInput) -> s.Post msg) )  