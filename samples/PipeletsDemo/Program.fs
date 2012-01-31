﻿module PipeletsDemo

open System
open System.Diagnostics
open System.Threading
open Fracture.Pipelets

let reverse (s:string) = 
    String(s |> Seq.toArray |> Array.rev)

let oneToSingleton a b f=
    let result = b |> f 
    result |> Seq.singleton

/// Total number to run through test cycle
let number = 1000

/// To Record when we are done
let counter = ref 0
let sw = new Stopwatch()
let countThis (a:String) =
    do Interlocked.Increment(counter) |> ignore
    if !counter % number = 0 then 
        sw.Stop()
        printfn "Execution time: %A" sw.Elapsed.TotalMilliseconds
        printfn "Items input: %d" number
        printfn "Time per item: %A ms (Elapsed Time / Number of items)" 
            (TimeSpan.FromTicks(sw.Elapsed.Ticks / int64 number).TotalMilliseconds)
        printfn "Press any key to repeat, press 'q' to exit."
        sw.Reset()
    counter |> Seq.singleton

let OneToSeqRev a b = 
    oneToSingleton a b reverse 

let generateCircularSeq (s) = 
    let rec next () = 
        seq {
            for element in s do
                yield element
            yield! next()
        }
    next()

let stage1 = new Pipelet<_,_>("Stage1", OneToSeqRev "1", Routers.roundRobin, number, -1)
let stage2 = new Pipelet<_,_>("Stage2", OneToSeqRev "2", Routers.basicRouter, number, -1)
let stage3 = new Pipelet<_,_>("Stage3", OneToSeqRev "3", Routers.basicRouter, number, -1)
let stage4 = new Pipelet<_,_>("Stage4", OneToSeqRev "4", Routers.basicRouter, number, -1)
let stage5 = new Pipelet<_,_>("Stage5", OneToSeqRev "5", Routers.basicRouter, number, -1)
let stage6 = new Pipelet<_,_>("Stage6", OneToSeqRev "6", Routers.basicRouter, number, -1)
let stage7 = new Pipelet<_,_>("Stage7", OneToSeqRev "7", Routers.basicRouter, number, -1)
let stage8 = new Pipelet<_,_>("Stage8", OneToSeqRev "8", Routers.basicRouter, number, -1)
let stage9 = new Pipelet<_,_>("Stage9", OneToSeqRev "9", Routers.basicRouter, number, -1)
let stage10 = new Pipelet<_,_>("Stage10", OneToSeqRev "10", Routers.basicRouter, number, -1)
let final = new Pipelet<_,_>("Final", countThis, Routers.basicRouter, number, -1)

let manyStages = [stage2;stage3;stage4;stage5;stage6;stage7;stage8;stage9;stage10]

oneToMany stage1 manyStages
manyToOne manyStages final

System.AppDomain.CurrentDomain.UnhandledException |> Observable.add (fun x -> 
    printfn "%A" (x.ExceptionObject :?> Exception); Console.ReadKey() |> ignore)

let circ = ["John"; "Paul"; "George"; "Ringo"; "Nord"; "Bert"] |> generateCircularSeq 

let startoperations() =
    sw.Start()
    for str in circ |> Seq.take number
        do  str --> stage1
    printfn "Insert complete waiting for operation to complete."

printfn "Press any key to process %i items" number
while not (Console.ReadKey().Key = ConsoleKey.Q) do
    startoperations()