// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Running;
using FxMap.Benchmark.FxMapBenchmarks.Reflections;

// BenchmarkRunner.Run<FxMapPropertyAccessorBenchmark>();
// BenchmarkRunner.Run<MappingBenchmark>();
// BenchmarkRunner.Run<MappablePropertiesBenchmark>(); // Old benchmark with Stack.Contains
BenchmarkRunner.Run<DiscoverResolvablePropertiesBenchmark>(); // New benchmark with HashSet
// BenchmarkRunner.Run<SetValueBenchmark>();