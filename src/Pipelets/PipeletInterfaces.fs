namespace Pipelets

    open System.Reflection
    [<assembly: AssemblyVersion("0.1.0.*")>] 
    do()
    
    [<Interface>]
    type IPipeletInput<'a> =
        abstract Insert: 'a -> unit 

    [<Interface>]
    type IPipeletConnect<'a> =
        abstract Attach: IPipeletInput<'a> -> unit
        abstract Detach: IPipeletInput<'a> -> unit

    [<Interface>]
    type IPipelet<'a,'b> =
        inherit IPipeletConnect<'b>
        inherit IPipeletInput<'a>

