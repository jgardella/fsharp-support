module Module

typeof<Foo> |> ignore
typeof<Sealed> |> ignore

Foo() |> ignore
Sealed() |> ignore

type T1() =
    inherit Foo()

type T2() =
    inherit Sealed()
