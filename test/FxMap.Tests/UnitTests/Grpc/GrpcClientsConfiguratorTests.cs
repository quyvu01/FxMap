using FxMap.Grpc.Registries;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Grpc;

/// <summary>The ways to list the hosts of the services that own the data.</summary>
public class GrpcClientsConfiguratorTests
{
    [Fact]
    public void One_host()
    {
        var configurator = new GrpcClientsConfigurator();

        configurator.AddGrpcHosts("http://a");

        configurator.ServiceHosts.ShouldBe(["http://a"]);
    }

    [Fact]
    public void Several_hosts_as_arguments()
    {
        var configurator = new GrpcClientsConfigurator();

        configurator.AddGrpcHosts("http://a", "http://b", "http://c");

        configurator.ServiceHosts.OrderBy(x => x).ShouldBe(["http://a", "http://b", "http://c"]);
    }

    [Fact]
    public void An_array_of_hosts()
    {
        var configurator = new GrpcClientsConfigurator();
        string[] hosts = ["http://a", "http://b"];

        configurator.AddGrpcHosts(hosts);

        configurator.ServiceHosts.OrderBy(x => x).ShouldBe(["http://a", "http://b"]);
    }

    [Fact]
    public void A_list_of_hosts_as_read_from_the_configuration()
    {
        var configurator = new GrpcClientsConfigurator();
        IEnumerable<string> hosts = new List<string> { "http://a", "http://b" }.Where(h => h.Length > 0);

        configurator.AddGrpcHosts(hosts);

        configurator.ServiceHosts.OrderBy(x => x).ShouldBe(["http://a", "http://b"]);
    }

    [Fact]
    public void A_host_given_twice_is_kept_once_and_the_calls_add_up()
    {
        var configurator = new GrpcClientsConfigurator();

        configurator.AddGrpcHosts("http://a", "http://b");
        configurator.AddGrpcHosts(["http://b", "http://c"]);

        configurator.ServiceHosts.OrderBy(x => x).ShouldBe(["http://a", "http://b", "http://c"]);
    }
}
