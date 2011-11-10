namespace Fracture

/// Agent alias for MailboxProcessor
type Agent<'a> = MailboxProcessor<'a>

/// One of three messages for our Object Pool agent
type PoolMessage<'a> =
    | Get of AsyncReplyChannel<'a>
    | Count of AsyncReplyChannel<int>
    | Put of 'a
    | Clear of AsyncReplyChannel<'a list>

/// Object pool representing a reusable pool of objects
type ObjectPool<'a>(generate: unit -> 'a, initialPoolCount, autoGrow) = 
    let initial = [ for x in 1..initialPoolCount do yield generate() ]
    let agent = Agent.Start(fun inbox ->
        let rec loop xs = async {
            let! msg = inbox.Receive()
            match msg with
            | Get(reply) -> 
                let res = match xs with
                          | hd::tl -> reply.Reply hd; tl
                          | [] as empty ->
                              // TODO: Create a state machine that checks the autoGrow argument and blocks additional access if it's full.
                              //if autoGrow then
                              reply.Reply (generate())
                              empty
                return! loop res
            | Count(reply) ->
                reply.Reply xs.Length
                return! loop xs
            | Put(x)-> 
                return! loop (x::xs) 
            | Clear(reply) -> 
                reply.Reply xs
                return! loop List.empty<'a> }
        loop initial)

    /// Creates an object pool that remains at a constant size
    new (generate, count) = ObjectPool<'a>(generate, count, false)

    /// Returns the number of items checked into the pool
    member this.Count() = agent.PostAndReply(Count)

    /// Returns the number of items checked into the pool
    member this.AsyncCount() = agent.PostAndAsyncReply(Count)

    /// Gets an item from the pool or if there are none present use the generator
    member this.Get(item) = agent.PostAndReply(Get)

    /// Gets an item from the pool or if there are none present use the generator
    member this.AsyncGet(item) = agent.PostAndAsyncReply(Get)

    /// Puts an item into the pool
    member this.Put(item) = agent.Post(Put item)

    /// Clears the object pool, returning all of the data that was in the pool.
    member this.ToListAndClear() = agent.PostAndReply(Clear)

    /// Clears the object pool, returning all of the data that was in the pool.
    member this.AsyncToListAndClear() = agent.PostAndAsyncReply(Clear)
