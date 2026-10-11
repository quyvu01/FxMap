using Xunit;

namespace FxMap.Tests.ProviderTests.Postgres;

/// <summary>The EF Core provider against a real PostgreSQL (set FXMAP_TEST_POSTGRES to run them).</summary>
[Collection(ProviderTestsCollection.Name)]
public class PostgresProviderTests(PostgresProviderFixture postgres) : IClassFixture<PostgresProviderFixture>
{
    [PostgresFact]
    public Task String_ids_are_answered_and_missing_ids_are_left_out() =>
        ProviderScenarios.String_ids_are_answered_and_missing_ids_are_left_out(postgres);

    [PostgresFact]
    public Task No_ids_gives_no_rows() => ProviderScenarios.No_ids_gives_no_rows(postgres);

    [PostgresFact]
    public Task The_same_id_twice_gives_one_row() => ProviderScenarios.The_same_id_twice_gives_one_row(postgres);

    [PostgresFact]
    public Task Navigation_and_aggregates_are_computed_by_the_database() =>
        ProviderScenarios.Navigation_and_aggregates_are_computed_by_the_database(postgres);

    [PostgresFact]
    public Task A_missing_nested_object_and_an_empty_collection_give_null_and_zero() =>
        ProviderScenarios.A_missing_nested_object_and_an_empty_collection_give_null_and_zero(postgres);

    [PostgresFact]
    public Task A_guid_id_is_answered_under_each_spelling_that_was_requested() =>
        ProviderScenarios.A_guid_id_is_answered_under_each_spelling_that_was_requested(postgres);

    [PostgresFact]
    public Task An_int_id_is_answered_under_each_spelling_that_was_requested() =>
        ProviderScenarios.An_int_id_is_answered_under_each_spelling_that_was_requested(postgres);

    [PostgresFact]
    public Task The_mapper_fills_a_view_from_the_database() =>
        ProviderScenarios.The_mapper_fills_a_view_from_the_database(postgres);

    [PostgresFact]
    public Task The_mapper_fills_a_view_through_a_computed_key() =>
        ProviderScenarios.The_mapper_fills_a_view_through_a_computed_key(postgres);
}
