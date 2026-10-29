// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Running;
using FxMap.Benchmark.Projection;

BenchmarkSwitcher.FromTypes([typeof(ProjectionBenchmark), typeof(EnrichPhaseBenchmark), typeof(LeafCollectionBenchmark), typeof(DeepChainBenchmark), typeof(IdPathBenchmark), typeof(FxMap.Benchmark.EngineOverhead.EngineOverheadBenchmark), typeof(FxMap.Benchmark.EngineOverhead.BuilderOverheadBenchmark)]).Run(args);
// BenchmarkRunner.Run<FxMapPropertyAccessorBenchmark>();
// BenchmarkRunner.Run<MappingBenchmark>();
// BenchmarkRunner.Run<MappablePropertiesBenchmark>(); // Old benchmark with Stack.Contains
// BenchmarkRunner.Run<SetValueBenchmark>();