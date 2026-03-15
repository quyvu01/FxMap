// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Running;
using FxMap.Benchmark.FxMapPropertyAssessors;

BenchmarkRunner.Run<FxMapPropertyAccessorBenchmark>();
// BenchmarkRunner.Run<MappingBenchmark>();
// BenchmarkRunner.Run<MappablePropertiesBenchmark>(); // Old benchmark with Stack.Contains
// BenchmarkRunner.Run<SetValueBenchmark>();