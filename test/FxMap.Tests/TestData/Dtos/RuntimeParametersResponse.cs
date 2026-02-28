using FxMap.Fluent;
using FxMap.Tests.TestData.Attributes;

namespace FxMap.Tests.TestData.Dtos;

/// <summary>
/// Test DTO demonstrating runtime parameters
/// </summary>
public class RuntimeParametersResponse
{
    public string CountryId { get; set; } = string.Empty;

    // Uses runtime parameters: ${index|0} and ${order|asc}
    public string ProvinceName { get; set; } = string.Empty;

    // Pagination with runtime parameters
    public List<ProvinceDto> Provinces { get; set; } = [];
}

public class RuntimeParametersResponseProfile : ProfileOf<RuntimeParametersResponse>
{
    protected override void Configure()
    {
        UseDistributedKey<CountryOfAttribute>()
            .Of(x => x.CountryId)
            .For(x => x.ProvinceName, "Provinces[${index|0} ${order|asc} Name].Name")
            .For(x => x.Provinces, "Provinces[${offset|0} ${limit|10} ${order|asc} Name]");
    }
}
