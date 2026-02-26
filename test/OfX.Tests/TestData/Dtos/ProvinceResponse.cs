using OfX.Fluent;
using OfX.Tests.TestData.Attributes;

namespace OfX.Tests.TestData.Dtos;

/// <summary>
/// Test DTO demonstrating collection operations
/// </summary>
public class ProvinceResponse
{
    public string Id { get; set; } = string.Empty;
    public string CountryId { get; set; } = string.Empty;

    public string CountryName { get; set; } = string.Empty;

    // Get first city (alphabetically)
    public string FirstCityName { get; set; } = string.Empty;

    // Get all cities in all provinces
    public List<ProvinceDto> AllProvinces { get; set; } = [];
}

public class ProvinceDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class ProvinceResponseProfile : ProfileOf<ProvinceResponse>
{
    protected override void Configure()
    {
        UseAnnotate<CountryOfAttribute>()
            .Of(x => x.CountryId)
            .For(x => x.CountryName)
            .For(x => x.FirstCityName, "Provinces[0 asc Name].Cities[0 asc Name].Name")
            .For(x => x.AllProvinces, "Provinces[asc Name]");
    }
}
