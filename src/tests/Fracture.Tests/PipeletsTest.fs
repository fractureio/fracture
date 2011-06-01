module Fracture.Pipelets.Tests

open Fracture.Pipelets
open NUnit.Framework
open FsUnit

let singletonRouter messages (routes:'a IPipeletInput seq) =
    let msg = Seq.head messages
    let route = Seq.head routes
    route.Post msg

let counter = ref 0
let finished = ref false
let finisher msg =
    incr counter
    finished := true
    Seq.singleton msg

let finish = new Pipelet<int,int>("Finish", finisher, singletonRouter, 1, -1)

[<Test>]
[<Ignore>]
let ``test should iterate once``() =
    finish.Post 1
    !counter |> should equal 1

[<Test>]
[<Ignore>]
let ``test should finish after one iteration``() =
    finish.Post 1
    !finished |> should be True
