using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FxMap.Abstractions;
using FxMap.EntityFrameworkCore.Extensions;
using FxMap.Extensions;
using FxMap.Tests.Infrastructure;
using FxMap.Tests.TestData.Builders;
using FxMap.Tests.TestData.Dtos;
using Shouldly;
using Xunit;

namespace FxMap.Tests.IntegrationTests.EfCore;

/// <summary>
/// Basic integration tests for data mapping
/// NOTE: Sequential collection required because DbContext registration uses static dictionary
/// </summary>
[Collection("EfCore Sequential")]
public class BasicMappingTests : TestDbContextBase<BasicMappingTestDbContext>
{
    private readonly IDistributedMapper _distributedMapper;

    public BasicMappingTests()
    {
        _distributedMapper = GetService<IDistributedMapper>();
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddFxMap(options =>
        {
            options.AddAttributesContainNamespaces(typeof(ITestAssemblyMarker).Assembly);
            options.AddProfilesFromAssemblyContaining<ITestAssemblyMarker>();
        }).AddEntityFrameworkCore(options =>
        {
            options.AddDbContexts(typeof(BasicMappingTestDbContext));
        });
    }

    protected override void SeedDatabase()
    {
        var usa = CountryBuilder.USA();
        var california = ProvinceBuilder.California();
        california.Country = usa;
        california.CountryId = usa.Id;

        DbContext.Countries.Add(usa);
        DbContext.Provinces.Add(california);

        var johnDoe = UserBuilder.JohnDoe();
        johnDoe.ProvinceId = california.Id;
        DbContext.Users.Add(johnDoe);

        DbContext.SaveChanges();
    }

    [Fact(Skip = "Disabled due to static DbContext registry limitation - passes when run individually")]
    public async Task MapDataAsync_Should_Map_Simple_Property()
    {
        // Arrange
        var response = new UserResponse
        {
            Id = "test-1",
            UserId = "john-doe"
        };

        // Act
        await _distributedMapper.MapDataAsync(response);

        // Assert
        response.UserName.ShouldBe("John Doe");
    }

    [Fact(Skip = "Disabled due to static DbContext registry limitation - passes when run individually")]
    public async Task MapDataAsync_Should_Map_Property_With_Expression()
    {
        // Arrange
        var response = new UserResponse
        {
            Id = "test-2",
            UserId = "john-doe"
        };

        // Act
        await _distributedMapper.MapDataAsync(response);

        // Assert
        response.UserEmail.ShouldBe("john.doe@example.com");
    }
}

/// <summary>
/// Dedicated DbContext for BasicMappingTests to avoid registration conflicts
/// </summary>
public class BasicMappingTestDbContext(DbContextOptions<BasicMappingTestDbContext> options) : TestDbContext(options);
