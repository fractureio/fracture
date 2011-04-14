namespace Pipelets
    open System.Collections.Concurrent
        
    type pipelet<'a,'b>(processor, router: seq<IPipeletInput<'b>> * 'b -> seq<IPipeletInput<'b>>, ?overflow, ?capacity, ?blockingTime) =
        let processor = processor
        let router = router

        let createBlockingCollection x =
                match x with
                | Some c -> new BlockingCollection<'a>(c:int)
                | None -> new BlockingCollection<'a>()

        let buffer = createBlockingCollection capacity
        let routes = ref List.empty<IPipeletInput<'b>>
        let queuedOrRunning = ref false

        let blocktime =
            match blockingTime with
            | Some b -> b
            | None -> 250
                
        let consumerLoop = async {
            try
                let rec loop()=
                    let item = ref Unchecked.defaultof<_>
                    let taken = buffer.TryTake(item, blocktime)
                    match taken with
                    | true ->
                          do !item 
                          |> processor 
                          |> Seq.iter (fun z -> 
                          (match !routes with
                           | [] -> ()(*we cant route with no routes*)
                           | _ -> do router (!routes, z) |> Seq.iter (fun r -> (r.Insert z ))) )
                          loop()
                    | false -> ()(*exit nothing to consume in time limit*)
                loop()
            with e -> raise e
            }

        member this.ClearRoutes = routes := []
        
        interface IPipeletInput<'a> with
            member this.Insert payload =
                let added = buffer.TryAdd(payload, blocktime)
                match added with
                | true -> 
                    //begin consumer loop
                    if not !queuedOrRunning then
                        lock consumerLoop (fun() ->
                        Async.Start(async {do! consumerLoop })
                        queuedOrRunning := true)
                    else()
                | false -> 
                    //overflow here if function passed
                    match overflow with
                    | Some t ->  payload |> overflow.Value 
                    | None -> ()
        
        interface IPipeletConnect<'b> with
            member this.Attach (stage) =
                let current = !routes
                routes := stage :: current

            member this.Detach (stage) =
                let current = !routes
                routes := List.filter (fun el -> el <> stage) current

        static member Attach (a:IPipeletConnect<_>) (b) = 
            a.Attach b
            b

        static member Detach (a: IPipeletConnect<_>) (b) = 
            a.Detach b
            a

        static member (++>) (a:IPipeletConnect<_>, b) = 
            a.Attach (b)
            b

        static member (-->) (a:IPipeletConnect<_>, b) = 
            a.Detach b
            a

        static member (<<--) (a:IPipeletInput<_>, b:'b) =
            a.Insert b

        static member (-->>) (b,a:IPipeletInput<_>) =
            a.Insert b