# Ideas from MapperR for FxMap: graph building and mapping traversal

> **Provenance (signature of this export).** Exported from the sibling repository `MapR`
> (branch `master`, commit `725f785`) on 2026-10-01 by a Claude Code session, at the repository owner's request.
> **Status: proposals, none validated in FxMap.** Every statement about FxMap below was read from this
> repository's source (file and method names are given so you can check them); every number about MapperR was
> measured in the MapR repository (see the appendix for the conditions). The C# in this document is a
> **signature sketch to validate**, not code that was compiled against FxMap.

## 0. How to read this

MapperR maps one object type to another (`CreateMap<Source, Destination>()`); FxMap enriches an object graph
with remote data. They solve different problems, so only the *techniques* around graphs transfer. Each idea
below has the same shape:

- **FxMap today** — what the code does now, with the place to look.
- **MapperR** — what it does instead, and the evidence.
- **Proposal and signatures** — what to try.
- **Validate** — how to find out whether it is worth it, and what would make you drop it.

FxMap spends most of a `MapDataAsync` call waiting for remote data. Before changing traversal code for speed,
**measure how much of a call the traversal is** (section 5, step 1). Ideas G1 and G6 are about correctness and
diagnostics and are worth considering whatever that measurement says.

| ID | Idea | FxMap place | Kind | Risk |
|----|------|-------------|------|------|
| G1 | Reject cycles in the property dependency graph | `ProfileOf.CollectDependencies` | correctness | low |
| G2 | A type-level graph of the models: contains resolvable? cyclic? | new | analysis | medium (polymorphism) |
| G3 | Use G2 to prune the object walk and to allocate `visited` lazily | `DistributedMapper.GetResolvablePropertiesRecursive` | speed | medium |
| G4 | Classify every property once, in one place | `IsPrimitiveType` call sites | maintainability | low |
| G5 | One session object per `MapDataAsync` instead of per-object lookups | `DistributedMapper` | speed, correctness | low |
| G6 | Validate the configuration when `AddFxMap` runs | `ProfileOf`, `MapConfigurator` | diagnostics | low |
| G7 | Make each optimization a flag; prove equivalence; measure as a ladder | tests, `FxMap.Benchmark` | method | none |

## 1. How FxMap builds and walks graphs today (facts)

1. **Property dependency graph, per model.** `ProfileOf<TModel>`'s constructor calls
   `BuildDependencyGraphFromFluentRules`, producing `DependencyGraphs : IReadOnlyDictionary<PropertyInfo,
   PropertyContext[]>`. For each target property the array is the chain *target ← selector ← selector's
   selector …*, collected by `CollectDependencies`. `PropertyInformation.Order` is the chain length minus one
   (`GetInformation`; `Extensions.GetPropertyOrder` repeats the same arithmetic).
2. **Silent cases in that build.** `CollectDependencies` returns `[]` when `visited.Add(property)` fails, so a
   cycle (`A`'s selector is `B`, `B`'s selector is `A`) produces a truncated chain and a plausible `Order`
   instead of an error. `BuildDependencyGraphFromFluentRules` also `continue`s silently when the selector
   property, the distributed key type or the target property is not found.
3. **Object walk, per call.** `DistributedMapper.MapDataAsync` loops level by level (up to
   `MaxNestingDepth`, default 128 in `MapConfigurator`). Each level calls `DiscoverResolvableProperties`,
   which creates a **new** `visited` set (reference equality) and walks the object graph through
   `GetResolvablePropertiesRecursive`: enumerables item by item (dictionaries by `Values`), objects through
   `profileConfig.Accessors`, recursing into every non-rule property value. The values of mapped non-primitive
   properties become the next level (`nextMappableData`).
4. **Profile lookup per object.** `GetResolvablePropertiesRecursive` and the `nextMappableData` aggregation call
   `serviceProvider.GetRequiredService<GetProfileConfig>()` for every object visited.
   `MapConfigurator.GetProfileConfig` creates a `VirtualProfileOf<T>` (no rules, accessors for every
   non-primitive property) on first sight of any CLR type, so the walk visits types that can never hold a rule.
5. **Accessors are already compiled** (`PropertyAccessor<T, TProp>` compiles getter and setter once), so the cost
   in item 4 is the lookup and the walk, not reflection.
6. **Profiles are found by runtime type** (`next.Model.GetType()`), so a property declared as a base type or an
   interface can hold an object with properties and rules the declared type does not have.

## 2. Ideas

### G1. Reject cycles in the property dependency graph

**FxMap today.** Section 1, item 2: a cyclic configuration is accepted and silently mis-ordered.

**MapperR.** The registry builds its graph from the same data the traversal reads, so every cycle is found
and classified; a cycle made only of explicit configuration is an *error* thrown when the configuration is
built, never silently cut (`WireProfileRegistry`, `ProfileValidator`, DESIGN #18/#19). A mistake in
configuration should fail where it is made.

**Proposal.** Each property has exactly one selector, so the graph is a forest of chains: following selectors
from a property either ends or returns to a property already on the chain. Detect the return and throw
`DistributedMapException.CircularPropertyDependency` naming the chain.

```csharp
// DistributedMapException.cs (nested, like the existing exceptions)
public sealed class CircularPropertyDependency(Type modelType, IReadOnlyList<string> propertyChain)
    : DistributedMapException(
        $"{modelType.Name}: property dependency cycle {string.Join(" -> ", propertyChain)}. " +
        "A property's selector cannot (indirectly) depend on the property itself.");

// ProfileOf.cs
private static void EnsureNoPropertyCycles(
    Type modelType, IReadOnlyDictionary<PropertyInfo, PropertyContext> directDeps);
```

**Validate.** Unit test: a profile whose rules make `A` depend on `B` and `B` on `A` throws, with the chain in the
message; the existing profiles in `FxMap.Tests` still build. Drop the idea only if some supported configuration
needs a cycle (none is documented).

### G2. A type-level graph of the models

**FxMap today.** Nothing answers "can anything beneath this type hold a rule?" or "can this type reach itself?";
both are discovered object by object, every call.

**MapperR.** `WireProfileRegistry` builds a graph of *types* once (nodes: pairs; edges: the nested members the
expression builder emits calls for — the same classification, so graph and traversal cannot disagree) and
answers `IsCyclic` (a node that can reach itself), `ReachesCycle` and `TracksReferences`. Cycle detection is a
depth-first reachability from each node. It decides, per type, whether reference tracking and a depth guard are
needed at all.

**Proposal.** A graph whose nodes are model types and whose edges are properties whose (element) type is a
model type (G4 decides which). Because FxMap resolves profiles by *runtime* type (section 1, item 6), a static
graph over declared types is only sound if it is **conservative**: a declared type that can hold a subtype
(not `sealed`, an interface, `object`) is `Unknown`, never `No`.

```csharp
namespace FxMap.Graphs;   // proposal

internal enum Reach { No, Yes, Unknown }

internal interface IModelTypeGraph
{
    /// <summary>Model types reachable through one property of <paramref name="modelType"/>.</summary>
    IReadOnlyCollection<Type> DependenciesOf(Type modelType);

    /// <summary>Whether the type itself, or anything beneath it, has at least one mapping rule.</summary>
    Reach ContainsResolvable(Type modelType);

    /// <summary>Whether an object of this type can reach another object of the same type.</summary>
    Reach IsCyclic(Type modelType);

    /// <summary>Whether this type, or anything beneath it, is cyclic.</summary>
    Reach ReachesCycle(Type modelType);

    /// <summary>Longest chain of nested models; null when <see cref="ReachesCycle"/> is not <see cref="Reach.No"/>.</summary>
    int? MaxStaticDepth(Type modelType);
}

internal sealed class ModelTypeGraph : IModelTypeGraph
{
    internal ModelTypeGraph(IEnumerable<Type> rootModelTypes, GetProfileConfig getProfileConfig,
        ModelTypeGraphOptions options);
}

internal sealed record ModelTypeGraphOptions
{
    /// <summary>When true (default), a declared type that is not sealed is Unknown: it may hold a subtype with rules.</summary>
    public bool TreatNonSealedAsUnknown { get; init; } = true;
}
```

**Validate.** Tests over small model sets: a leaf DTO (`No`), a DTO holding a rule (`Yes`), a self-reference and two
types referencing each other (cyclic), a property declared `object` or as an interface (`Unknown`), a non-sealed
base type that has a subclass with a rule (`Unknown`, never `No`). **The risk to measure:** with
`TreatNonSealedAsUnknown = true` most hand-written DTOs are `Unknown`, which could make G3 prune nothing;
count, on a real model set, how many types come out `No`.

### G3. Use G2 to prune the walk and to allocate `visited` lazily

**FxMap today.** Section 1, items 3 and 4: every non-primitive property value of every object is visited and
looked up, and a new `HashSet<object>` is allocated per level.

**MapperR.** Pairs that cannot reach a cycle skip tracking and the depth guard and share one stateless context
instead of allocating one per call; pairs that can reach one keep both. Measured: skipping the per-call context
removed 86 ns (of 309) on the no-cycle graph (appendix).

**Proposal.** In `GetResolvablePropertiesRecursive`: when the declared property type is `ContainsResolvable ==
Reach.No`, do not read its value or recurse. Allocate `visited` only when the root type is not
`IsCyclic == Reach.No`.

**Validate.** Semantics first: an object shared by two parents in an **acyclic** graph is visited once with
`visited` and twice without it; the second visit produces duplicate `PropertyDescriptor`s. The fetch already
dedupes selector ids and expressions, and the set is idempotent, so the expected result is the same data with
a little redundant work — **prove it with a test that compares the mapped graph with and without the
optimization** (G7). Then benchmark (section 5). Drop it if few types are `No` (G2's risk) or if the
traversal is a small fraction of a call.

### G4. Classify every property once, in one place

**FxMap today.** "Is this property a leaf?" is `GeneralHelpers.IsPrimitiveType`, used where the walk, the
accessor creation (`ProfileOf` constructor: `nonPrimitiveProperties`) and `nextMappableData` each need it;
collections and dictionaries are handled inline in the walk.

**MapperR.** `MemberClassifier` is the only place that decides what a member is (`Direct`, `Numeric`, `ToText`,
`Nested`, `Collection`, `Invalid`). The graph and the expression builder both read its answer. Before it, they
re-derived the answer separately and disagreed, which produced a real stack overflow; one classifier removed
that class of bug.

```csharp
namespace FxMap.Graphs;   // proposal

internal enum PropertyKind
{
    Leaf,              // primitive, string, enum, struct, ...
    Resolvable,        // has a mapping rule
    Nested,            // a model object
    NestedCollection,  // IEnumerable<T> / array of model objects (string excluded)
    NestedDictionary   // IDictionary<TKey, TValue> whose values are model objects
}

internal static class PropertyClassifier
{
    internal static PropertyKind Classify(PropertyInfo property, bool hasRule);

    /// <summary>Element type of an enumerable (string excluded), the value type of a dictionary; null otherwise.</summary>
    internal static Type ElementTypeOf(Type type);
}
```

**Validate.** Behavior-preserving refactor: the existing tests pass unchanged; add a test per `PropertyKind`.

### G5. One session object per `MapDataAsync`

**FxMap today.** Section 1, items 3 and 4: `GetProfileConfig` is requested from the service provider for every object
visited; `visited` is per level; depth and `ThrowIfExceptions` are read from configuration inside the loop.

**MapperR.** One `MappingContext` per top-level call carries the depth guard, the visited objects and the
settings, and is passed down; a lookup that was a dictionary access per nested object (instead of a cached
slot) made the plain runtime engine about a quarter slower (655 ns against 521 ns on the no-cycle graph; appendix).

**Proposal.** Two steps, the first nearly free:

1. Resolve `GetProfileConfig` and `IMapperConfiguration` **once** (constructor injection instead of three
   `GetRequiredService<GetProfileConfig>()` calls inside loops).
2. A session object holding them, a profile cache per type, and a `visited` set shared across levels.

```csharp
namespace FxMap.Implementations;   // proposal

internal sealed class MappingSession(IMapperConfiguration configuration, GetProfileConfig getProfileConfig)
{
    internal int Depth { get; set; }

    /// <summary>First visit of this object in the whole call? Reference equality, shared across levels.</summary>
    internal bool TryVisit(object model);

    /// <summary>The profile of a runtime type, looked up once per type per call.</summary>
    internal IFluentProfileConfig ProfileOf(Type modelType);

    internal bool ExceededMaxDepth => Depth >= configuration.MaxNestingDepth;
}
```

**Validate.** Step 1: benchmark `MapDataAsync` on a graph of a few thousand objects before and after.
Step 2 changes semantics (a `visited` shared across levels): **hypothesis to test** — can an object returned by a
remote handler already be in `visited` from an earlier level, and must it be mapped again? If yes, keep per-level
`visited`.

### G6. Validate the configuration when `AddFxMap` runs

**FxMap today.** Section 1, item 2 (silent skips and cycles). `MaxNestingDepth` is a bare number; nothing says
whether the configured models can ever need it.

**MapperR.** An invalid explicit member is an *error* thrown when its pair is built; an invalid convention member
is a *warning* and is skipped; `CreateMap` rejects nonsense at the call (`ProfileValidator`), so mistakes
surface at configuration, not deep inside a mapping.

**Proposal.** Collect diagnostics while building profiles and the graph, and throw once with all errors:

```csharp
namespace FxMap.Graphs;   // proposal

internal enum DiagnosticSeverity { Warning, Error }

internal sealed record MapperDiagnostic(DiagnosticSeverity Severity, Type ModelType, string Member, string Message);

internal interface IMapperConfigurationValidator
{
    IReadOnlyList<MapperDiagnostic> Validate(IMapperConfiguration configuration, IModelTypeGraph graph);
}
```

Candidate checks: a rule whose selector property or target property is not found (today a silent `continue`) —
Error; a property dependency cycle (G1) — Error; `MaxNestingDepth` smaller than `MaxStaticDepth` of an acyclic
model — Warning ("the configured depth cannot reach the deepest model").

**Validate.** One test per check; make sure no existing configuration in `FxMap.Tests` or the samples starts
failing for a reason that was intentional. Consider also surfacing the same checks as `FxMap.Analyzers`
diagnostics where the information is available at compile time.

### G7. Each optimization a flag; prove equivalence; measure as a ladder

This is the method that made MapperR's optimizations trustworthy, and it transfers whatever you decide about G3
and G5.

```csharp
[Flags]
internal enum TraversalOptimizations
{
    None = 0,
    HoistProfileLookup = 1,        // G5 step 1
    PruneTypesWithoutResolvables = 2,   // G3
    LazyVisited = 4,               // G3
    SessionVisited = 8,            // G5 step 2
    All = 15,
    Default = None                 // switch on one by one, as each is proven
}
```

- **Equivalence test.** A theory over every combination of flags: map the same input graphs (with cycles, shared
  references, empty and null members) with `None` and with the combination, and compare the results serialized
  with `ReferenceHandler.Preserve`, so *which objects are shared* is compared, not only values.
- **Ladder benchmark.** One `[MemoryDiagnoser]` class, one benchmark per cumulative combination, one
  provider per row so each row runs through the real registrations; read each row against the one above it.
- **Run each benchmark with several launches** (`--launchCount 3` or more): BenchmarkDotNet's error column
  covers one process only (appendix).

## 3. What not to port

- **Nested pairs resolved at runtime through a context (MapperR's "Approach A").** It exists because MapperR
  builds one expression tree per type *pair* and inlining pairs recurses at build time. FxMap has no pairs.
- **Code generation (`maprgen`), `CollectionMapper`, `AllowNullCollections`.** They answer MapperR-specific
  questions (printing the same tree as C#, mapping `Map<Dst[]>(list)`, null collections).
- **Collection destination rules.** FxMap writes remote values into existing properties; it does not build
  destination collections from sources.

## 4. Signatures at a glance

Everything proposed above, in one place for review (namespaces are suggestions):

```csharp
// --- G1 ---------------------------------------------------------------- FxMap.Exceptions
public sealed class CircularPropertyDependency(Type modelType, IReadOnlyList<string> propertyChain)
    : DistributedMapException(/* message names the chain */);

// ProfileOf<TModel>
private static void EnsureNoPropertyCycles(Type modelType, IReadOnlyDictionary<PropertyInfo, PropertyContext> directDeps);

// --- G4 ---------------------------------------------------------------- FxMap.Graphs
internal enum PropertyKind { Leaf, Resolvable, Nested, NestedCollection, NestedDictionary }
internal static class PropertyClassifier
{
    internal static PropertyKind Classify(PropertyInfo property, bool hasRule);
    internal static Type ElementTypeOf(Type type);
}

// --- G2 ---------------------------------------------------------------- FxMap.Graphs
internal enum Reach { No, Yes, Unknown }
internal interface IModelTypeGraph
{
    IReadOnlyCollection<Type> DependenciesOf(Type modelType);
    Reach ContainsResolvable(Type modelType);
    Reach IsCyclic(Type modelType);
    Reach ReachesCycle(Type modelType);
    int? MaxStaticDepth(Type modelType);
}
internal sealed class ModelTypeGraph : IModelTypeGraph
{
    internal ModelTypeGraph(IEnumerable<Type> rootModelTypes, GetProfileConfig getProfileConfig, ModelTypeGraphOptions options);
}
internal sealed record ModelTypeGraphOptions { public bool TreatNonSealedAsUnknown { get; init; } = true; }

// --- G5 ---------------------------------------------------------------- FxMap.Implementations
internal sealed class MappingSession(IMapperConfiguration configuration, GetProfileConfig getProfileConfig)
{
    internal int Depth { get; set; }
    internal bool TryVisit(object model);
    internal IFluentProfileConfig ProfileOf(Type modelType);
    internal bool ExceededMaxDepth { get; }
}

// --- G6 ---------------------------------------------------------------- FxMap.Graphs
internal enum DiagnosticSeverity { Warning, Error }
internal sealed record MapperDiagnostic(DiagnosticSeverity Severity, Type ModelType, string Member, string Message);
internal interface IMapperConfigurationValidator
{
    IReadOnlyList<MapperDiagnostic> Validate(IMapperConfiguration configuration, IModelTypeGraph graph);
}

// --- G7 ---------------------------------------------------------------- FxMap.Implementations
[Flags]
internal enum TraversalOptimizations
{
    None = 0, HoistProfileLookup = 1, PruneTypesWithoutResolvables = 2, LazyVisited = 4, SessionVisited = 8, All = 15, Default = None
}
```

## 5. Validation checklist (in order)

1. **Baseline first.** Using `test/FxMap.Benchmark` (or a copy), measure `MapDataAsync` on (a) a wide graph with
   thousands of objects, most of them types without rules, (b) a deep graph near `MaxNestingDepth`, (c) a graph with
   a reference cycle. Record time **and** allocations, with several launches. Use a profiler to see what share of
   a call is the walk: if it is small, skip G3 and G5 step 2 and keep G1, G4, G6.
2. **G1** (cycle detection): unit test, then run the whole existing suite.
3. **G4** (classifier): refactor with no behavior change; the existing suite must pass untouched.
4. **G6** (diagnostics): one test per check; confirm the samples still start.
5. **G2** (type graph): tests from G2's Validate list; then **count how many real types are `No`** with
   `TreatNonSealedAsUnknown` on and off.
6. **G5 step 1** (hoist the lookup) and re-measure.
7. **G3, G5 step 2** only if steps 1 and 5 say they can pay: add the flags (G7), the equivalence theory first, then
   the benchmark ladder, one flag at a time.

## Appendix: MapperR evidence and where to read it

All measured with BenchmarkDotNet 0.15.8 on .NET 10, Apple Silicon (arm64), three launches per benchmark unless
stated. The graph used is a company with departments and employees that reference each other (cycles, shared
objects, nested value types, several collection shapes). AutoMapper 14 is the comparison point.

| Cumulative step (MapperR runtime engine) | Order + 10 lines | Organisation, 4 employees | Organisation, 211 employees |
|---|---|---|---|
| plain | 501 ns | 3,442 ns | 135.8 µs |
| + collection element delegates hoisted | 309 ns | 1,379 ns | 75.1 µs |
| + one shared stateless context for pairs that cannot reach a cycle | 223 ns | 1,415 ns | 71.1 µs |
| + pairs outside any cycle built into the parent's tree | 131 ns | 1,213 ns | 60.0 µs |
| AutoMapper 14 | 203 ns | 2,166 ns | 105 µs |

Lessons that carry over regardless of the numbers:

- **BenchmarkDotNet's error column covers one process.** The same configuration measured 98 µs in one single-launch
  run and 77 µs in another, and a step first appeared 15 % slower than the one before it; with three launches the
  inversion disappeared. Conclusions need `--launchCount 3` or more.
- **Measure through the real registrations.** A resolver that looked pairs up in a dictionary on every nested call
  made the plain engine look about a quarter slower (655 ns against 521 ns) than the cached lookup used in applications.
- **A lookup per object adds up** (the basis of G5), but measure it in FxMap before assuming it matters there.
- **Prove equivalence for every flag combination**, comparing identity (`ReferenceHandler.Preserve`), before trusting
  any speed number.

Where to read the MapperR side (repository `MapR`, sibling of this one):

| Topic | File |
|---|---|
| Type-level graph, `IsCyclic`, `ReachesCycle`, `TracksReferences` | `src/MapperR.Core/Registries/WireProfileRegistry.cs` |
| One classifier for every member | `src/MapperR.Core/Helpers/MemberClassifier.cs` |
| Per-call context: depth guard, visited objects, shared stateless variant | `src/MapperR.Core/Abstractions/MappingContext.cs` |
| Choosing a context per mapper | `src/MapperR.Core/Implementations/ContextKind.cs` |
| Optimization flags and the rewrites | `src/MapperR.Core/Implementations/MapperOptimizations.cs`, `RuntimeExpressionOptimizer.cs` |
| Rejecting meaningless `CreateMap` at the call | `src/MapperR.Core/Helpers/ProfileValidator.cs` |
| Equivalence theory over flag combinations | `tests/MapperR.Core.Tests/UnitTests/ComplexMappingTests.cs` |
| Ladder benchmark | `tests/Benchmark/OptimizationLadderBenchmarks.cs` |
| Design decisions with their alternatives (local only, not in git) | `docs/DESIGN.md` |
