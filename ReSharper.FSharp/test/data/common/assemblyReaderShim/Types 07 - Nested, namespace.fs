module Module

typeof<Ns.Foo> |> ignore
typeof<Ns.Foo.Nested> |> ignore

open Ns

typeof<Foo> |> ignore
typeof<Foo.Nested> |> ignore
