# fsharp-hedgehog-xunit

[Hedgehog][hedgehog] with attributes for [xUnit.net][xunit].

<img src="https://github.com/hedgehogqa/fsharp-hedgehog/raw/master/img/hedgehog-logo.png" width="307" align="right"/>

## Features

- Test method arguments generated by the customizable [`GenX.auto`](https://github.com/hedgehogqa/fsharp-hedgehog-experimental/#auto-generation).
- `Property.check` called for each test.

## Motivating example

The following test uses xUnit, Hedgehog, and [Hedgehog.Experimental](https://github.com/hedgehogqa/fsharp-hedgehog-experimental):

```f#
open Xunit
open Hedgehog
[<Fact>]
let ``Reversing a list twice yields the original list`` () =
  property {
    let! xs = GenX.auto<int list>
    return List.rev (List.rev xs) = xs
  } |> Property.check
```

This library, `Hedgehog.Xunit`, allows us to simplify the above test to:

```f#
open Hedgehog.Xunit
[<Property>]
let ``Reversing a list twice yields the original list, with Hedgehog.Xunit`` (xs: int list) =
  List.rev (List.rev xs) = xs
```

## Documentation

`Hedgehog.Xunit` provides the `Property` attribute and the `Properties` attribute.

### `Property` attribute

Methods with the `Property` attribute have their arguments are generated by [`GenX.auto`](https://github.com/hedgehogqa/fsharp-hedgehog-experimental/#auto-generation).

```f#
type ``class with a test`` (output: Xunit.Abstractions.ITestOutputHelper) =
  [<Property>]
  let ``Can generate an int`` (i: int) =
    output.WriteLine $"Test input: {i}"
	
=== Output ===
Test input: 0
Test input: -1
Test input: 1
...
Test input: 522317518
Test input: 404306656
Test input: 1550509078
```

`Property.check` is also run.

```f#
[<Property>]
let ``This test fails`` (b: bool) =
  b

=== Output ===
Hedgehog.FailedException: *** Failed! Falsifiable (after 2 tests):
(false)
```

If the test returns an `Async<unit>`, `Task`, or `Task<unit>`, `Async.RunSynchronously` or `.GetAwaiter().GetResult()` will be called, _which blocks the thread._ This may have significant performance implications as tests run 100 times by default.

```f#
[<Property>]
let ``AsyncBuilder with exception shrinks`` (i: int) = async {
  do! Async.Sleep 100
  if i > 10 then
    raise <| Exception()
  }

=== Output ===
Hedgehog.FailedException: *** Failed! Falsifiable (after 12 tests):
(11)
```

There are 3 options: `AutoGenConfig`, `Tests` (count), and `Skip` (reason).

#### `AutoGenConfig`

* Default: `GenX.defaults`

Create a class with a single static property that returns an instance of `AutoGenConfig`. Then provide the type of this class as an argument to the `Property` attribute. This works around the constraint that [`Attribute` parameters must be a constant.](https://stackoverflow.com/a/33007272)

```f#
type AutoGenConfigContainer =
  static member __ =
    { GenX.defaults with
        Int = Gen.constant 13 }

[<Property(typeof<AutoGenConfigContainer>)>]
let ``This test passes`` (i: int) =
  i = 13
```

#### `Tests` (count)

* Default: `100<tests>`

Specifies the number of tests to be run, though more or less may occur due to shrinking or early failure.

```f#
[<Property(3<tests>)>]
let ``This runs 3 times`` () =
  ()
```

#### `Skip` (reason)

* Default: `null`

This is the same as the [`Skip`](https://github.com/xunit/xunit/blob/v2/src/xunit.core/FactAttribute.cs) on `[<Fact>]`.

```f#
[<Property("just because")>]
let ``This test is skipped`` () =
  ()
```

### `Properties` attribute

This optional attribute may decorate modules or classes. It sets default arguments for `AutoGenConfig` and `Tests`. These will be overridden by any arguments provided by the `Property` attribute.

```f#
type Int13   = static member __ = { GenX.defaults with Int = Gen.constant 13   }
type Int2718 = static member __ = { GenX.defaults with Int = Gen.constant 2718 }

[<Properties(typeof<Int13>, 1<tests>)>]
module ``Module with <Properties> tests`` =

  [<Property>]
  let ``this passes and runs once`` (i: int) =
    i = 13

  [<Property(typeof<Int2718>, 2<tests>)>]
  let ``this passes and runs twice`` (i: int) =
    i = 2718
```

## Tips

Use named arguments to select the desired constructor overload.

```f#
[<Properties(Tests = 13<tests>, AutoGenConfig = typeof<AutoGenConfigContainer>)>]
module __ =
  [<Property(AutoGenConfig = typeof<AutoGenConfigContainer>, Tests = 2718<tests>, Skip = "just because")>]
  let ``Not sure why you'd do this, but okay`` () =
    ()
```

Types which inherit from `PropertyAttribute` xor `PropertiesAttribute` will retain their parent's arguments.

```f#
type Int13 = static member __ = { GenX.defaults with Int = Gen.constant 13 }

type PropertyInt13Attribute() = inherit PropertyAttribute(typeof<Int13>)
module __ =
  [<PropertyInt13>]
  let ``this passes`` (i: int) =
    i = 13

type PropertiesInt13Attribute() = inherit PropertiesAttribute(typeof<Int13>)
[<PropertiesInt13>]
module ___ =
  [<Property>]
  let ``this also passes`` (i: int) =
    i = 13
```

[hedgehog]: https://github.com/hedgehogqa/fsharp-hedgehog
[xunit]: https://xunit.net/
