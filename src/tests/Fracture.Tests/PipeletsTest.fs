module Fracture.Tests.PipeletsTest

open Fracture.Pipelets
open NUnit.Framework
open FsUnit

[<Test>]
[<Explicit>]
let ``test basicRouter should increment once with no attached stages``() =
    let counter = ref 0
    let run msg =
        incr counter
        Seq.singleton msg
    
    let start = new Pipelet<int,int>("Start", run, basicRouter, 1, -1)
    async {
        start.Post 1
        // Give the post a chance to complete
        do! Async.Sleep 100
        return !counter |> should equal 1
    } |> Async.RunSynchronously

[<Test>]
[<Explicit>]
let ``test basicRouter should increment twice with one attached stage``() =
    let counter = ref 0
    let run msg =
        incr counter
        Seq.singleton msg
    
    let start = new Pipelet<int,int>("Start", run, basicRouter, 1, -1)
    let finish1 = new Pipelet<int,int>("Finish1", run, basicRouter, 1, -1)

    start ++> finish1 |> ignore

    async {
        start.Post 1
        // Give the post a chance to complete
        do! Async.Sleep 100
        return !counter |> should equal 2
    } |> Async.RunSynchronously

[<Test>]
[<Explicit>]
let ``test basicRouter should increment twice with two attached stages``() =
    let counter = ref 0
    let run msg =
        incr counter
        Seq.singleton msg
    
    let start = new Pipelet<int,int>("Start", run, basicRouter, 1, -1)
    let finish1 = new Pipelet<int,int>("Finish1", run, basicRouter, 1, -1)
    let finish2 = new Pipelet<int,int>("Finish2", run, basicRouter, 1, -1)

    start ++> finish1 |> ignore
    start ++> finish2 |> ignore

    async {
        start.Post 1
        // Give the post a chance to complete
        do! Async.Sleep 100
        return !counter |> should equal 2
    } |> Async.RunSynchronously

[<Test>]
[<Explicit>]
let ``test multicastRouter should increment once for the start and the one attached stage``() =
    let counter = ref 0
    let run msg =
        incr counter
        Seq.singleton msg
    
    let start = new Pipelet<int,int>("Start", run, multicastRouter, 1, -1)
    let finish1 = new Pipelet<int,int>("Finish1", run, multicastRouter, 1, -1)
    let finish2 = new Pipelet<int,int>("Finish2", run, multicastRouter, 1, -1)

    start ++> finish1 |> ignore
    start ++> finish2 |> ignore

    async {
        start.Post 1
        // Give the post a chance to complete
        do! Async.Sleep 100
        return !counter |> should equal 3
    } |> Async.RunSynchronously

[<Test>]
[<Explicit>]
let ``test multicastRouter should increment once for the start and both attached stages``() =
    let counter = ref 0
    let run msg =
        incr counter
        Seq.singleton msg
    
    let start = new Pipelet<int,int>("Start", run, multicastRouter, 1, -1)
    let finish1 = new Pipelet<int,int>("Finish1", run, multicastRouter, 1, -1)
    let finish2 = new Pipelet<int,int>("Finish2", run, multicastRouter, 1, -1)

    start ++> finish1 |> ignore
    start ++> finish2 |> ignore

    async {
        start.Post 1
        // Give the post a chance to complete
        do! Async.Sleep 100
        return !counter |> should equal 3
    } |> Async.RunSynchronously

[<Test>]
[<Explicit>]
let ``test multicastRouter should increment once for the start and all attached stages``() =
    let counter = ref 0
    let run msg =
        incr counter
        Seq.singleton msg
    
    let start = new Pipelet<int,int>("Start", run, multicastRouter, 1, -1)
    let finish1 = new Pipelet<int,int>("Finish1", run, multicastRouter, 1, -1)
    let finish2 = new Pipelet<int,int>("Finish2", run, multicastRouter, 1, -1)
    let finish3 = new Pipelet<int,int>("Finish3", run, multicastRouter, 1, -1)

    start ++> finish1 |> ignore
    start ++> finish2 |> ignore
    start ++> finish3 |> ignore

    async {
        start.Post 1
        // Give the post a chance to complete
        do! Async.Sleep 100
        return !counter |> should equal 4
    } |> Async.RunSynchronously
