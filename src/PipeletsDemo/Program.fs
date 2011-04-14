module PipeletsDemo
    open System
    open Pipelets

    let consoleLock = new obj()

    let split del n (s:string) = 
        lock consoleLock (fun() -> 
        do printfn "%A:before split %A" n s
        let split = s.Split([|del|]) 
        do printfn "%A:after: split into: %A" n split
        split |> Array.toSeq)

    let reverse (s:string) = 
        new string(s |> Seq.toArray |> Array.rev)

    let oneToSingleton a b f=
        lock consoleLock (fun() -> 
            printfn "%A:before reverse %A" a b
            let result = b |> f 
            printfn "%A:after reverse %A" a result
            result|> Seq.singleton)

    let OneToSeqRev a b = oneToSingleton a b reverse 

    ///Simply picks the first route
    let basicRouter( r, i) =
        let head = Seq.head r
        Seq.singleton head
    
    let p1 = pipelet( split ',' "1", basicRouter)
    let p2 = pipelet( OneToSeqRev "2", basicRouter)
    let p3 = pipelet( OneToSeqRev "3", basicRouter)

    p1 ++> p2 ++> p3 |> ignore

    let generateCircularSeq (lst:'a list) = 
        let rec next () = 
            seq {
                for element in lst do
                    yield element
                yield! next()
            }
        next()

    for str in ["John,Paul,George,Ringo"; "Nord,Bert"] 
    |> generateCircularSeq 
    |> Seq.take 10 
        do  str -->> p1

    let x = Console.ReadKey()