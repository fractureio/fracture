module Fracture.Pipelets.Tests

open Fracture.Pipelets
open NUnit.Framework
open FsUnit

let singletonRouter messages (routes:'a IPipeletInput seq) =
    let msg = Seq.head messages
    let route = Seq.head routes
    route.Post msg

[<Test>]
let ``test should iterate once``() =
    let counter = ref 0
    let run msg =
        incr counter
        Seq.singleton msg
    
    let finish = new Pipelet<int,int>("Finish", run, singletonRouter, 1, -1)
    async {
        finish.Post 1
        // Give the post a chance to complete
        do! Async.Sleep 500
        return !counter |> should equal 1
    } |> Async.RunSynchronously

[<Test>]
let ``test should finish after one iteration``() =
    let finished = ref false
    let run msg =
        finished := true
        Seq.singleton msg
    
    let finish = new Pipelet<int,int>("Finish", run, singletonRouter, 1, -1) 
    async {
        finish.Post 1
        // Give the post a chance to complete
        do! Async.Sleep 500
        return !finished |> should be True
    } |> Async.RunSynchronously
    