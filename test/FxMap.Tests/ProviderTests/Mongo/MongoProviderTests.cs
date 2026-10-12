using FxMap.Tests.ProviderTests;
using Shouldly;
using Xunit;

namespace FxMap.Tests.ProviderTests.Mongo;

/// <summary>The MongoDB data provider against a real MongoDB (set FXMAP_TEST_MONGO to run them).</summary>
[Collection(ProviderTestsCollection.Name)]
public class MongoProviderTests(MongoProviderFixture mongo) : IClassFixture<MongoProviderFixture>
{
    [MongoFact]
    public Task String_ids_are_answered_and_missing_ids_are_left_out() =>
        ProviderScenarios.String_ids_are_answered_and_missing_ids_are_left_out(mongo);

    [MongoFact]
    public Task No_ids_gives_no_rows() => ProviderScenarios.No_ids_gives_no_rows(mongo);

    [MongoFact]
    public Task The_same_id_twice_gives_one_row() => ProviderScenarios.The_same_id_twice_gives_one_row(mongo);

    [MongoFact]
    public Task Navigation_and_aggregates_are_computed_by_the_database() =>
        ProviderScenarios.Navigation_and_aggregates_are_computed_by_the_database(mongo);

    [MongoFact]
    public Task A_missing_nested_object_and_an_empty_collection_give_null_and_zero() =>
        ProviderScenarios.A_missing_nested_object_and_an_empty_collection_give_null_and_zero(mongo);

    [MongoFact]
    public Task A_guid_id_is_answered_under_each_spelling_that_was_requested() =>
        ProviderScenarios.A_guid_id_is_answered_under_each_spelling_that_was_requested(mongo);

    [MongoFact]
    public Task An_int_id_is_answered_under_each_spelling_that_was_requested() =>
        ProviderScenarios.An_int_id_is_answered_under_each_spelling_that_was_requested(mongo);

    [MongoFact]
    public Task The_mapper_fills_a_view_from_the_database() => ProviderScenarios.The_mapper_fills_a_view_from_the_database(mongo);

    [MongoFact]
    public async Task A_computed_id_is_not_supported_by_the_provider()
    {
        // Documents the limit: MongoDB works with stored field names, so Id(x => x.Mrn + x.Code) cannot be filtered.
        var error = await Should.ThrowAsync<NotSupportedException>(() =>
            mongo.RunAsync<ProviderLabKey>(["MRN0001GLU"], [null]));

        error.Message.ShouldContain("property selectors");
    }

    [MongoFact]
    public Task Expressions_that_cannot_be_built_are_reported_and_the_others_answered() =>
        ProviderScenarios.Expressions_that_cannot_be_built_are_reported_and_the_others_answered(mongo);

    [MongoFact]
    public Task The_errors_are_reported_even_when_no_row_matches() =>
        ProviderScenarios.The_errors_are_reported_even_when_no_row_matches(mongo);

    [MongoFact]
    public Task The_mapper_throws_with_the_error_of_a_broken_expression() =>
        ProviderScenarios.The_mapper_throws_with_the_error_of_a_broken_expression(mongo);
}
