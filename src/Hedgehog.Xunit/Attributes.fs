namespace Hedgehog.Xunit

open System
open Xunit.Sdk
open Hedgehog

/// Generates arguments using GenX.auto (or autoWith if you provide an AutoGenConfig), then runs Property.check
[<AttributeUsage(AttributeTargets.Method ||| AttributeTargets.Property, AllowMultiple = false)>]
[<XunitTestCaseDiscoverer("XunitOverrides+PropertyTestCaseDiscoverer", "Hedgehog.Xunit")>]
type PropertyAttribute(autoGenConfig, tests, shrinks, size) =
  inherit Xunit.FactAttribute()

  let mutable _autoGenConfig: Type         option = autoGenConfig
  let mutable _tests        : int<tests>   option = tests
  let mutable _shrinks      : int<shrinks> option = shrinks
  let mutable _size         : Size         option = size

  new()                                   = PropertyAttribute(None              , None      , None        , None)
  new(tests)                              = PropertyAttribute(None              , Some tests, None        , None)
  new(tests, shrinks)                     = PropertyAttribute(None              , Some tests, Some shrinks, None)
  new(autoGenConfig)                      = PropertyAttribute(Some autoGenConfig, None      , None        , None)
  new(autoGenConfig:Type, tests)          = PropertyAttribute(Some autoGenConfig, Some tests, None        , None)
  new(autoGenConfig:Type, tests, shrinks) = PropertyAttribute(Some autoGenConfig, Some tests, Some shrinks, None)

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
  member          _.Shrinks          with set v = _shrinks       <- Some v
  member          _.Size             with set v = _size          <- Some v
  member internal _.GetAutoGenConfig            = _autoGenConfig
  member internal _.GetTests                    = _tests
  member internal _.GetShrinks                  = _shrinks
  member internal _.GetSize                     = _size


/// Set a default AutoGenConfig or <tests> for all [<Property>] attributed methods in this class/module
[<AttributeUsage(AttributeTargets.Class, AllowMultiple = false)>]
type PropertiesAttribute(autoGenConfig, tests, shrinks, size) =
  inherit Attribute() // don't inherit Property - it exposes members like Skip which are unsupported

  let mutable _autoGenConfig: Type         option = autoGenConfig
  let mutable _tests        : int<tests>   option = tests
  let mutable _shrinks      : int<shrinks> option = shrinks
  let mutable _size         : Size         option = size

  new()                                   = PropertiesAttribute(None              , None      , None        , None)
  new(tests)                              = PropertiesAttribute(None              , Some tests, None        , None)
  new(tests, shrinks)                     = PropertiesAttribute(None              , Some tests, Some shrinks, None)
  new(autoGenConfig)                      = PropertiesAttribute(Some autoGenConfig, None      , None        , None)
  new(autoGenConfig:Type, tests)          = PropertiesAttribute(Some autoGenConfig, Some tests, None        , None)
  new(autoGenConfig:Type, tests, shrinks) = PropertiesAttribute(Some autoGenConfig, Some tests, Some shrinks, None)

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
  member          _.Shrinks          with set v = _shrinks       <- Some v
  member          _.Size             with set v = _size          <- Some v
  member internal _.GetAutoGenConfig            = _autoGenConfig
  member internal _.GetTests                    = _tests
  member internal _.GetShrinks                  = _shrinks
  member internal _.GetSize                     = _size

/// Runs Property.reportRecheck
[<AttributeUsage(AttributeTargets.Method ||| AttributeTargets.Property, AllowMultiple = false)>]
type RecheckAttribute(size, value, gamma) =
  inherit Attribute()

  let _size  : int    = size
  let _value : uint64 = value
  let _gamma : uint64 = gamma

  member internal _.GetSize  = _size
  member internal _.GetValue = _value
  member internal _.GetGamma = _gamma
