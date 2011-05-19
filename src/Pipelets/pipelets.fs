namespace Pipelets
    open System
    open System.Reflection
    open System.Collections.Concurrent
    open FSharp.Control

    [<AutoOpen>]
    module AsyncOperators =
        let inline (>>=) m f = async.Bind(m, f)
        let inline mreturn x = async.Return(x)
    
    type pipelet<'a,'b>(processor, router: seq<IPipeletInput<'b>> * 'b -> seq<IPipeletInput<'b>>, capacity, ?overflow, ?blockingTime) =
        let buffer = BlockingQueueAgent<_> capacity
        let routes = ref List.empty<IPipeletInput<'b>>
        let queuedOrRunning = ref false
        let blocktime = defaultArg blockingTime 250
        let consumerlock = new Object()

        let getandprocess =
            let exec () = buffer.AsyncTryGet(blocktime) >>= (mreturn << Option.map processor)
            async.Delay(exec) // This is the important delay point.

        let consumerloop =
            let terminate() = queuedOrRunning := false; mreturn ()
            let rec loop () = getandprocess >>= exec
            and exec result =
                match result with
                | Some(x) ->
                    do x |> Seq.iter (fun z -> 
                        match !routes with 
                        | [] -> ()
                        | _ -> do router(!routes, z) |> Seq.iter (fun r -> r.Insert z ))
                    loop()
                | _ -> lock consumerlock terminate
            loop()
                
        member this.ClearRoutes = routes := []
        
        interface IPipeletInput<'a> with
            member this.Insert payload =
                let errorHandler e = mreturn <| match overflow with Some(f) -> f payload | _ -> ()
                let start() = Async.Start(consumerloop); queuedOrRunning := true
                let exec result =
                    match result with
                    // Start consumer loop
                    | Some(x) -> mreturn <| if not !queuedOrRunning then lock consumerlock start
                    | _ -> errorHandler()
                let computation = buffer.AsyncTryAdd(payload, blocktime) >>= exec
                async.TryWith(computation, errorHandler) |> Async.Start
        
        interface IPipeletConnect<'b> with
            member this.Attach (stage) =
                let current = !routes
                routes := stage :: current
            member this.Detach (stage) =
                let current = !routes
                routes := List.filter (fun el -> el <> stage) current

        static member Attach (a:IPipeletConnect<_>) (b) = a.Attach b;b
        static member Detach (a: IPipeletConnect<_>) (b) = a.Detach b;a
        ///Connect operator
        static member (++>) (a:IPipeletConnect<_>, b) = a.Attach (b);b
        ///Detach operator
        static member (-->) (a:IPipeletConnect<_>, b) = a.Detach b;a
        ///Insert into leftoperator
        static member (<<--) (a:IPipeletInput<_>, b:'b) = a.Insert b
        ///Insert into right operator
        static member (-->>) (b,a:IPipeletInput<_>) = a.Insert b