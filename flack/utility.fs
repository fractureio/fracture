module utility

    let runIfSome a b =
        match b with
        | Some(b) -> a |> b
        | None -> ()

    let ( |?> ) = runIfSome