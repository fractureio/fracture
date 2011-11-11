namespace Fracture

open System.Collections.Generic

type Agent<'a> = MailboxProcessor<'a>

// [snippet: ObjectPool messages]
/// One of three messages for our Object Pool agent
type PoolMessage<'a> =
    | Get of AsyncReplyChannel<'a>
    | Put of 'a
    | Clear
// [/snippet]

// [snippet: ObjectPool]
/// Object pool representing a reusable pool of objects
type ObjectPool<'a>(initialPoolCount, generate: unit -> 'a, ?cleanUp, ?autoGrow) = 
    [<VolatileField>]
    let mutable count = 0
    let autoGrow = defaultArg autoGrow false
    let cleanUp = defaultArg cleanUp ignore

    let agent = Agent.Start(fun inbox ->
        let stack = new Stack<_>(initialPoolCount)
        let rec initialize() =
            for _ in 0 .. initialPoolCount - 1 do
                stack.Push(generate())
            count <- initialPoolCount
            chooseState()
        and loop() = async {
            let! msg = inbox.Receive()
            match msg with
            | Get(reply)   -> return! popAndContinue(reply)
            | Put(x)       -> return! pushAndContinue(x)
            | Clear        -> return! clearAndContinue() }
        and emptyStack() =
            inbox.Scan(fun msg ->
                match msg with
                | Put(x)       -> Some(pushAndContinue(x))
                | Clear        -> Some(clearAndContinue())
                | _ -> None)
        and popAndContinue(reply) =
            reply.Reply(stack.Pop())
            count <- count - 1
            chooseState()
        and pushAndContinue(x) =
            stack.Push(x)
            count <- count + 1
            chooseState()
        and clearAndContinue() =
            Seq.iter cleanUp stack
            stack.Clear()
            emptyStack()
        and chooseState() =
            if count = 0 then
                if autoGrow then
                    stack.Push(generate())
                    loop()
                else emptyStack()
            else loop()
                
        initialize())

    /// Returns the count of items in the pool
    member this.Count = count

    /// Gets an item from the pool or if there are none present use the generator
    member this.Get(item) = agent.PostAndReply(Get)

    /// Gets an item from the pool or if there are none present use the generator
    member this.AsyncGet(item) = agent.PostAndAsyncReply(Get)

    /// Puts an item into the pool
    member this.Put(item) = agent.Post(Put item)

    /// Clears the object pool, returning all of the data that was in the pool.
    member this.Clear() = agent.Post(Clear)
// [/snippet]

#if INTERACTIVE
// [snippet: ObjectPool usage examples]
(*
`ObjectPool` is intended to be a functional variation on the idea of a `BlockingCollection`. It is a slight variation
on the `BlockingQueueAgent`, the primary differences being the use of a `Stack` rather than a `Queue` and the availability
of both `Count` and `Clear` messages. The `Clear` method uses the `cleanUp` parameter to clean up the stack before clearing.
*)
    let generate() = obj()

    // Compare `ConcurrentStack` with `ObjectPool`.
    let stack = System.Collections.Concurrent.ConcurrentStack<_>([| for _ in 1..100000 do yield generate() |])
    let pool = ObjectPool<_>(100000, generate, autoGrow = true)

    // Get the items.
    let stackItems = [| for _ in 1..100000 do
                          let item = ref null
                          if stack.TryPop(item) then
                              yield !item |]
    let poolItems = [| for _ in 1..100000 do yield pool.Get() |]

    // Put the items back.
    for x in stackItems do stack.Push(x)
    for x in poolItems do pool.Put(x)

    // Check the counts.
    let stackCount = stack.Count
    let poolCount = pool.Count

    // Get one item.
    let stackItem = ref null
    stack.TryPop(stackItem)
    !stackItem
    let poolItem = pool.Get()

    // Compare `BlockingCollection` with `ObjectPool`.
    let collection = new System.Collections.Concurrent.BlockingCollection<obj>(100000)
    for _ in 1..100000 do collection.Add(generate())
    let boundedPool = ObjectPool<_>(100000, generate)

    // Get the items.
    let collectionItems = [| for _ in 1..100000 do yield collection.Take() |]
    let boundedPoolItems = [| for _ in 1..100000 do yield boundedPool.Get() |]

    // Put the items back.
    for x in collectionItems do collection.Add(x)
    for x in boundedPoolItems do boundedPool.Put(x)

    // Check the counts.
    let collectionCount = collection.Count
    let boundedPoolCount = boundedPool.Count
// [/snippet]
#endif
