module Module

typeof<Foo> |> ignore
typeof<Foo.Nested> |> ignore

Foo() |> ignore
Foo.Nested() |> ignore
