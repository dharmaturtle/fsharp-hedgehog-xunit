namespace Hedgehog.Xunit

open System
open Xunit.Sdk
open Hedgehog

/// Generates arguments using GenX.auto (or autoWith if you provide an AutoGenConfig), then runs Property.check
[<AttributeUsage(AttributeTargets.Method ||| AttributeTargets.Property, AllowMultiple = false)>]
[<XunitTestCaseDiscoverer("Hedgehog.Xunit.XunitOverrides+PropertyTestCaseDiscoverer", "Hedgehog.Xunit")>]
type PropertyAttribute(autoGenConfig, tests, skip) =
  inherit Xunit.FactAttribute(Skip = skip)

  let mutable _autoGenConfig: Type       option = autoGenConfig
  let mutable _tests        : int<tests> option = tests

  new()                     = PropertyAttribute(None              , None      , null)
  new(autoGenConfig)        = PropertyAttribute(Some autoGenConfig, None      , null)
  new(autoGenConfig, tests) = PropertyAttribute(Some autoGenConfig, Some tests, null)
  new(tests, autoGenConfig) = PropertyAttribute(Some autoGenConfig, Some tests, null)
  new(autoGenConfig, skip)  = PropertyAttribute(Some autoGenConfig, None      , skip)
  new(tests)                = PropertyAttribute(None              , Some tests, null)
  new(skip)                 = PropertyAttribute(None              , None      , skip)

  // https://github.com/dotnet/fsharp/issues/4154 sigh
  /// This requires a type with a single static member (with any name) that returns an AutoGenConfig.
  ///
  /// Example usage:
  ///
  /// ```
  ///
  /// type Int13 = static member AnyName = GenX.defaults |> AutoGenConfig.addGenerator (Gen.constant 13)
  ///
  /// [<Property(typeof<Int13>)>]
  ///
  /// let myTest (i:int) = ...
  ///
  /// ```
  member          _.AutoGenConfig    with set v = _autoGenConfig <- Some v
  member          _.Tests            with set v = _tests         <- Some v
  member internal _.GetAutoGenConfig            = _autoGenConfig
  member internal _.GetTests                    = _tests


/// Set a default AutoGenConfig or <tests> for all [<Property>] attributed methods in this class/module
[<AttributeUsage(AttributeTargets.Class, AllowMultiple = false)>]
type PropertiesAttribute(autoGenConfig, tests) =
  inherit Attribute() // don't inherit Property - it exposes members like Skip which are unsupported

  let mutable _autoGenConfig: Type       option = autoGenConfig
  let mutable _tests        : int<tests> option = tests

  new()                          = PropertiesAttribute(None              , None)
  new(autoGenConfig)             = PropertiesAttribute(Some autoGenConfig, None      )
  new(tests)                     = PropertiesAttribute(None              , Some tests)
  new(autoGenConfig:Type, tests) = PropertiesAttribute(Some autoGenConfig, Some tests)
  new(tests, autoGenConfig:Type) = PropertiesAttribute(Some autoGenConfig, Some tests)

  // https://github.com/dotnet/fsharp/issues/4154 sigh
  /// This requires a type with a single static member (with any name) that returns an AutoGenConfig.
  ///
  /// Example usage:
  ///
  /// ```
  ///
  /// type Int13 = static member AnyName = GenX.defaults |> AutoGenConfig.addGenerator (Gen.constant 13)
  ///
  /// [<Property(typeof<Int13>)>]
  ///
  /// let myTest (i:int) = ...
  ///
  /// ```
  member          _.AutoGenConfig    with set v = _autoGenConfig <- Some v
  member          _.Tests            with set v = _tests         <- Some v
  member internal _.GetAutoGenConfig            = _autoGenConfig
  member internal _.GetTests                    = _tests

module internal PropertyHelper =

  module Option =
    let requireSome msg =
      function
      | Some x -> x
      | None   -> failwith msg
  let (++) (x: 'a option) (y: 'a option) =
    match x with
    | Some _ -> x
    | None -> y

  open System.Reflection
  type private Marker = class end
  let private genxAutoBoxWith<'T> x = x |> GenX.autoWith<'T> |> Gen.map box
  let private genxAutoBoxWithMethodInfo =
    typeof<Marker>.DeclaringType.GetTypeInfo().GetDeclaredMethod(nameof genxAutoBoxWith)

  let parseAttributes (testMethod:MethodInfo) (testClass:Type) =
    let classAutoGenConfig, classTests =
      testClass.GetCustomAttributes(typeof<PropertiesAttribute>)
      |> Seq.tryExactlyOne
      |> Option.map (fun x -> x :?> PropertiesAttribute)
      |> function
      | Some x -> x.GetAutoGenConfig, x.GetTests
      | None   -> None              , None
    let configType, tests =
      testMethod.GetCustomAttributes(typeof<PropertyAttribute>)
      |> Seq.exactlyOne
      :?> PropertyAttribute
      |> fun methodAttribute ->
        methodAttribute.GetAutoGenConfig ++ classAutoGenConfig,
        methodAttribute.GetTests         ++ classTests        |> Option.defaultValue 100<tests>
    let config =
      match configType with
      | None -> GenX.defaults
      | Some t ->
        t.GetProperties()
        |> Seq.filter (fun p ->
          p.GetMethod.IsStatic &&
          p.GetMethod.ReturnType = typeof<AutoGenConfig>
        ) |> Seq.tryExactlyOne
        |> Option.requireSome $"{t.FullName} must have exactly one static property that returns an {nameof AutoGenConfig}.

An example type definition:

type {t.Name} =
  static member __ =
    GenX.defaults |> AutoGenConfig.addGenerator (Gen.constant 13)
"       |> fun x -> x.GetMethod.Invoke(null, [||])
        :?> AutoGenConfig
    config, tests

  let dispose (o:obj) =
    match o with
    | :? IDisposable as d -> d.Dispose()
    | _ -> ()

  let resultIsOk r =
    match r with
    | Ok _ -> true
    | Error _ -> false

  open System.Threading.Tasks
  open System.Threading
  open System.Linq
  let report (testMethod:MethodInfo) testClass testClassInstance =
    let config, tests = parseAttributes testMethod testClass
    let gens =
      testMethod.GetParameters()
      |> Array.mapi (fun i p ->
        if p.ParameterType.ContainsGenericParameters then
          invalidArg p.Name $"The parameter type '{p.ParameterType.Name}' at index {i} is generic, which is unsupported. Consider using a type annotation to make the parameter's type concrete."
        genxAutoBoxWithMethodInfo
          .MakeGenericMethod(p.ParameterType)
          .Invoke(null, [|config|])
        :?> Gen<obj>)
      |> List.ofArray
      |> ListGen.sequence
    let rec toProperty (x: obj) =
      match x with
      | :? bool        as b -> Property.ofBool b
      | :? Task<unit>  as t -> Async.AwaitTask t |> toProperty
      | _ when x <> null && x.GetType().IsGenericType && x.GetType().GetGenericTypeDefinition().IsSubclassOf typeof<Task> ->
        typeof<Async>
          .GetMethods()
          .First(fun x -> x.Name = nameof Async.AwaitTask && x.IsGenericMethod)
          .MakeGenericMethod(x.GetType().GetGenericArguments())
          .Invoke(null, [|x|])
        |> toProperty
      | :? Task        as t -> Async.AwaitTask t |> toProperty
      | :? Async<unit> as a -> Async.RunSynchronously(a, cancellationToken = CancellationToken.None) |> toProperty
      | _ when x <> null && x.GetType().IsGenericType && x.GetType().GetGenericTypeDefinition() = typedefof<Async<_>> ->
        typeof<Async> // Invoked with Reflection because we can't cast an Async<MyType> to Async<obj> https://stackoverflow.com/a/26167206
          .GetMethod(nameof Async.RunSynchronously)
          .MakeGenericMethod(x.GetType().GetGenericArguments())
          .Invoke(null, [| x; None; Some CancellationToken.None |])
        |> toProperty
      | _ when x <> null && x.GetType().IsGenericType && x.GetType().GetGenericTypeDefinition() = typedefof<Result<_,_>> ->
        typeof<Marker>
          .DeclaringType
          .GetTypeInfo()
          .GetDeclaredMethod(nameof resultIsOk)
          .MakeGenericMethod(x.GetType().GetGenericArguments())
          .Invoke(null, [|x|])
        |> toProperty
      | _                        -> Property.success ()
    let invoke args =
      try
        testMethod.Invoke(testClassInstance, args |> Array.ofList)
        |> toProperty
      finally
        List.iter dispose args
        
    Property.forAll gens invoke |> Property.report' tests


module internal XunitOverrides =
  type PropertyTestInvoker  (test, messageBus, testClass, constructorArguments, testMethod, testMethodArguments, beforeAfterAttributes, aggregator, cancellationTokenSource) =
    inherit XunitTestInvoker(test, messageBus, testClass, constructorArguments, testMethod, testMethodArguments, beforeAfterAttributes, aggregator, cancellationTokenSource)
  
    override this.CallTestMethod testClassInstance =
      PropertyHelper.report this.TestMethod this.TestClass testClassInstance
      |> Report.tryRaise
      null
  
  type PropertyTestRunner  (test, messageBus, testClass, constructorArguments, testMethod, testMethodArguments, skipReason, beforeAfterAttributes, aggregator, cancellationTokenSource) =
    inherit XunitTestRunner(test, messageBus, testClass, constructorArguments, testMethod, testMethodArguments, skipReason, beforeAfterAttributes, aggregator, cancellationTokenSource)
  
    override this.InvokeTestMethodAsync aggregator =
      PropertyTestInvoker(this.Test, this.MessageBus, this.TestClass, this.ConstructorArguments, this.TestMethod, this.TestMethodArguments, this.BeforeAfterAttributes, aggregator, this.CancellationTokenSource)
        .RunAsync()
  
  type PropertyTestCaseRunner(testCase: IXunitTestCase, displayName, skipReason, constructorArguments, testMethodArguments, messageBus, aggregator, cancellationTokenSource) =
    inherit XunitTestCaseRunner(testCase,               displayName, skipReason, constructorArguments, testMethodArguments, messageBus, aggregator, cancellationTokenSource)
  
    override this.RunTestAsync() =
      let args = this.TestMethod.GetParameters().Length |> Array.zeroCreate // need to pass the right number of args otherwise an exception will be thrown by XunitTestInvoker's InvokeTestMethodAsync, whose behavior I don't feel like overriding.
      PropertyTestRunner(this.CreateTest(this.TestCase, this.DisplayName), this.MessageBus, this.TestClass, this.ConstructorArguments, this.TestMethod, args, this.SkipReason, this.BeforeAfterAttributes, this.Aggregator, this.CancellationTokenSource)
        .RunAsync()
  
  open System.ComponentModel
  type PropertyTestCase  (diagnosticMessageSink, defaultMethodDisplay, testMethodDisplayOptions, testMethod, ?testMethodArguments) =
    inherit XunitTestCase(diagnosticMessageSink, defaultMethodDisplay, testMethodDisplayOptions, testMethod, (testMethodArguments |> Option.defaultValue null))
  
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    [<Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")>]
    [<System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage>]
    new() = new PropertyTestCase(null, TestMethodDisplay.ClassAndMethod, TestMethodDisplayOptions.All, null)
  
    override this.RunAsync(_, messageBus, constructorArguments, aggregator, cancellationTokenSource) =
      PropertyTestCaseRunner(this, this.DisplayName, this.SkipReason, constructorArguments, this.TestMethodArguments, messageBus, aggregator, cancellationTokenSource)
        .RunAsync()
  
  type PropertyTestCaseDiscoverer(messageSink) =
  
    interface IXunitTestCaseDiscoverer with
      override _.Discover(discoveryOptions, testMethod, _) =
        new PropertyTestCase(messageSink, discoveryOptions.MethodDisplayOrDefault(), discoveryOptions.MethodDisplayOptionsOrDefault(), testMethod)
        :> IXunitTestCase
        |> Seq.singleton
