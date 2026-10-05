using BenchmarkDotNet.Attributes;
using FxMap.Helpers;
using FxMap.Responses;
using FxMap.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FxMap.Benchmark.Projection;

/// <summary>
/// The id path of a data provider: converting the id texts of a request, and answering them the way the caller wrote
/// them. Written so it compiles against both shapes of IdConverter / RequestedIdAnswers (it only uses their results).
/// </summary>
[MemoryDiagnoser]
public class IdPathBenchmark
{
    [Params(1, 10, 100, 1000)] public int Count { get; set; }

    private string[] _guidIds = null!;
    private string[] _upperGuidIds = null!;
    private string[] _stringIds = null!;
    private string[] _intIds = null!;
    private DataResponse[] _rows = null!;
    private IdConverter<Guid> _guidConverter = null!;
    private IdConverter<string> _stringConverter = null!;
    private IdConverter<int> _intConverter = null!;

    [GlobalSetup]
    public void Setup()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        _guidConverter = new IdConverter<Guid>(sp);
        _stringConverter = new IdConverter<string>(sp);
        _intConverter = new IdConverter<int>(sp);
        var guids = Enumerable.Range(0, Count).Select(_ => Guid.NewGuid()).ToArray();
        _guidIds = guids.Select(g => g.ToString()).ToArray();
        _upperGuidIds = guids.Select(g => g.ToString().ToUpperInvariant()).ToArray();
        _stringIds = Enumerable.Range(0, Count).Select(i => $"id-{i}").ToArray();
        _intIds = Enumerable.Range(0, Count).Select(i => i.ToString()).ToArray();
        _rows = _guidIds.Select(id => new DataResponse
        {
            Id = id, Values = [new ValueResponse { Expression = "Name", Value = "\"n\"" }]
        }).ToArray();
    }

    [Benchmark]
    public object Convert_Guid() => _guidConverter.ConvertIds(_guidIds);

    [Benchmark]
    public object Convert_String() => _stringConverter.ConvertIds(_stringIds);

    [Benchmark]
    public object Convert_Int() => _intConverter.ConvertIds(_intIds);

    /// <summary>Every requested text is already the canonical id: nothing to translate.</summary>
    [Benchmark]
    public object Answer_Canonical() => RequestedIdAnswers.Align(_guidIds, _rows, _guidConverter);

    /// <summary>The caller wrote the ids in upper case: every one has to be translated.</summary>
    [Benchmark]
    public object Answer_OtherSpelling() => RequestedIdAnswers.Align(_upperGuidIds, _rows, _guidConverter);
}
