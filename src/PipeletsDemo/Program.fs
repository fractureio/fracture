module File1
open System
open System.Diagnostics
open System.Threading
open fracture

let reverse (s:string) = 
    new string(s |> Seq.toArray |> Array.rev)

let oneToSingleton a b f=
        let result = b |> f 
        result |> Seq.singleton

/// total number to run through test cycle
let number = 100000

/// hack to record when we are done
let counter = ref 0
let sw = new Stopwatch()
let countthis (a:String) =
    do Interlocked.Increment(counter) |> ignore
    if !counter % number = 0 then 
        sw.Stop()
        printfn "Execution time: %A" sw.Elapsed
        printfn "Items input: %d" number
        printfn "Time per item: %A ms (Elapsed Time / Number of items)" (TimeSpan.FromTicks(sw.ElapsedTicks / int64 number).TotalMilliseconds)
        printfn "Press a key to exit."
    counter|> Seq.singleton

let OneToSeqRev a b = 
    //Console.WriteLine(sprintf "stage: %s item: %s" a b)
    oneToSingleton a b reverse 

/// Simply picks the first route
let basicRouter( r, i) =
    r|> Seq.head |> Seq.singleton

let generateCircularSeq (s) = 
    let rec next () = 
        seq {
            for element in s do
                yield element
            yield! next()
        }
    next()
             
let stage1 = pipelet("Stage1", OneToSeqRev "1", basicRouter, number, -1)
let stage2 = pipelet("Stage2", OneToSeqRev "2", basicRouter, number, -1)
let stage3 = pipelet("Stage3", OneToSeqRev "3", basicRouter, number, -1)
let stage4 = pipelet("Stage4", OneToSeqRev "4", basicRouter, number, -1)
let stage5 = pipelet("Stage5", OneToSeqRev "5", basicRouter, number, -1)
let stage6 = pipelet("Stage6", OneToSeqRev "6", basicRouter, number, -1)
let stage7 = pipelet("Stage7", OneToSeqRev "7", basicRouter, number, -1)
let stage8 = pipelet("Stage8", OneToSeqRev "8", basicRouter, number, -1)
let stage9 = pipelet("Stage9", OneToSeqRev "9", basicRouter, number, -1)
let stage10 = pipelet("Stage10", OneToSeqRev "10", basicRouter, number, -1)
let final = pipelet("Final", countthis, basicRouter, number, -1)

stage1 
++> stage2
++> stage3
++> stage4 
++> stage4 
++> stage5 
++> stage6 
++> stage7 
++> stage8 
++> stage9 
++> stage10 
++> final 
++> {new IPipeletInput<_> with member this.Post payload = () }|> ignore

//remove stage2 from stage1
//stage1 -+> stage2 |> ignore
      
System.AppDomain.CurrentDomain.UnhandledException |> Observable.add (fun x -> 
    printfn "%A" (x.ExceptionObject :?> Exception);Console.ReadKey() |>ignore)

sw.Start()
for str in ["John"; "Paul"; "George"; "Ringo"; "Nord"; "Bert"] 
|> generateCircularSeq 
|> Seq.take number
    do  str --> stage1

Console.WriteLine("Insert complete waiting for operation to complete.")
let x = Console.ReadKey()
