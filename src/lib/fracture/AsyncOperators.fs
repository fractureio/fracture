module Fracture.Async

let inline (>>=) m f = async.Bind(m, f)
let inline (<*>) f m = f >>= fun f' -> m >>= fun m' -> async.Return(f' m')
let inline pipe m f = m >>= fun m' -> async.Return(f m')
let inline pipe2 x y f = async.Return f <*> x <*> y
let inline pipe3 x y z f = async.Return f <*> x <*> y <*> z
let inline (<!>) f m = pipe m f
let inline ( *>) x y = pipe2 x y (fun _ z -> z)
let inline ( <*) x y = pipe2 x y (fun z _ -> z)
