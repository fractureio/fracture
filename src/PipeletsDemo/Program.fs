module PipeletsDemo
    open System
    open Pipelets

    let split del n (s:string) = 
        Console.WriteLine(sprintf "%s: Before split %A" n s)
        let split = s.Split([|del|]) 
        Console.WriteLine(sprintf "%s: After: split into: %A" n split)
        split |> Array.toSeq

    let reverse (s:string) = 
        new string(s |> Seq.toArray |> Array.rev)

    let oneToSingleton a b f=
            Console.WriteLine(sprintf "%s: Before reverse %A" a b)
            let result = b |> f 
            Console.WriteLine(sprintf "%s: After reverse %A" a result)
            result|> Seq.singleton

    let OneToSeqRev a b = oneToSingleton a b reverse 

    ///Simply picks the first route
    let basicRouter( r, i) =
        r|> Seq.head |> Seq.singleton

    let printoverflow stageno msg =
        let str = sprintf"%s: Overflow: %A" stageno msg
        Console.WriteLine(str)
    
    let p1 = pipelet(split ',' "1", basicRouter, 5, printoverflow "1", 1000)
    let p2 = pipelet(OneToSeqRev "2", basicRouter, 5, printoverflow "2", 1000)
    let p3 = pipelet(OneToSeqRev "3", basicRouter, 5, printoverflow "3", 1000)
    let p4 = pipelet(OneToSeqRev "4", basicRouter, 5, printoverflow "4", 1000)
    let p5 = pipelet(OneToSeqRev "5", basicRouter, 5, printoverflow "5", 1000)
    let p6 = pipelet(OneToSeqRev "6", basicRouter, 5, printoverflow "6", 1000)
    let p7 = pipelet(OneToSeqRev "7", basicRouter, 5, printoverflow "7", 1000)
    let p8 = pipelet(OneToSeqRev "8", basicRouter, 5, printoverflow "8", 1000)
    let p9 = pipelet(OneToSeqRev "9", basicRouter, 5, printoverflow "9", 1000)
    let p10 = pipelet(OneToSeqRev "10", basicRouter, 5, printoverflow "10", 1000)

    //composition of stages
    p1 ++> p2 ++> p3 ++> p4 ++> p5++> p6++> p7++> p8++> p9++> p10 |> ignore

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

    for str in ["John,Paul,George,Ringo"; "Nord,Bert"] 
    |> generateCircularSeq 
    |> Seq.take 10
        do  str -->> p1

    let x = Console.ReadKey()