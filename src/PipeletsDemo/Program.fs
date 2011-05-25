module File1
open System
open System.Diagnostics
open System.Threading
open fracture

let reverse (s:string) = 
    new string(s |> Seq.toArray |> Array.rev)

let oneToSingleton a b f=
        //Console.WriteLine(sprintf "%s: Before reverse %A" a b)
        let result = b |> f 
        //Console.WriteLine(sprintf "%s: After reverse %A" a result)
        result |> Seq.singleton

///total number to run through test cycle
let number = 10000

///dirty hack to record when we are done
let counter = ref 0
let sw = new Stopwatch()
let countthis (a:String) =
    do Interlocked.Increment(counter) |> ignore
    if !counter % number = 0 then 
        sw.Stop()
        printfn "Execution time: %A" sw.Elapsed
    counter|> Seq.singleton

let OneToSeqRev a b = oneToSingleton a b reverse 

///Simply picks the first route
let basicRouter( r, i) =
    r|> Seq.head |> Seq.singleton
              
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
let stage11 = pipelet("Stage11", OneToSeqRev "11", basicRouter, number, -1)
let stage12 = pipelet("Stage12", OneToSeqRev "12", basicRouter, number, -1)
let stage13 = pipelet("Stage13", OneToSeqRev "13", basicRouter, number, -1)
let stage14 = pipelet("Stage14", OneToSeqRev "14", basicRouter, number, -1)
let stage15 = pipelet("Stage15", OneToSeqRev "15", basicRouter, number, -1)
let stage16 = pipelet("Stage16", OneToSeqRev "16", basicRouter, number, -1)
let stage17 = pipelet("Stage17", OneToSeqRev "17", basicRouter, number, -1)
let stage18 = pipelet("Stage18", OneToSeqRev "18", basicRouter, number, -1)
let stage19 = pipelet("Stage19", OneToSeqRev "19", basicRouter, number, -1)
let stage20 = pipelet("Stage20", OneToSeqRev "20", basicRouter, number, -1)
let final = pipelet("Final", countthis, basicRouter, number, -1)

//construction is long winded as there are no symbolic ops yet...
stage1.Attach(stage2)
stage2.Attach(stage3)
stage3.Attach(stage4)
stage4.Attach(stage5)
stage5.Attach(stage6)
stage6.Attach(stage7)
stage7.Attach(stage8)
stage8.Attach(stage9)
stage9.Attach(stage10)
stage10.Attach(stage11)
stage11.Attach(stage12)
stage12.Attach(stage13)
stage13.Attach(stage14)
stage14.Attach(stage15)
stage15.Attach(stage16)
stage16.Attach(stage17)
stage17.Attach(stage18)
stage18.Attach(stage19)
stage19.Attach(stage20)
stage20.Attach(final)

// note object expression to fake up last stage as no routing 
// will take place if nothing is attached to final stage
// at the moment this is an optimisation i.e. if there id nothing attached
// then why transform, let alone route, nee to think about use cases on this....
final.Attach(  {new IPipeletInput<_> with member this.Post payload = () })
   
let generateCircularSeq (lst:'a list) = 
    let rec next () = 
        seq {
            for element in lst do
                yield element
            yield! next()
        }
    next()
    
System.AppDomain.CurrentDomain.UnhandledException |> Observable.add (fun x -> 
    printfn "%A" (x.ExceptionObject :?> Exception);Console.ReadKey() |>ignore)

sw.Start()
for str in ["John"; "Paul"; "George"; "Ringo"; "Nord"; "Bert"] 
|> generateCircularSeq 
|> Seq.take number
    do  str --> stage1

Console.WriteLine("Press a key to exit.")
let x = Console.ReadKey()
