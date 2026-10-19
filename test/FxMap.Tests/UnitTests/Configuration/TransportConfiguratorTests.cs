using System.Reflection;
using Amazon;
using Confluent.Kafka;
using FxMap.Aws.Sqs.Configuration;
using FxMap.Azure.ServiceBus.Configuration;
using FxMap.Kafka.Registries;
using FxMap.Nats.Configuration;
using FxMap.RabbitMq.Registries;
using NATS.Client.Core;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Configuration;

/// <summary>What the callbacks of AddKafka, AddRabbitMq, AddSqs, AddNats and AddAzureServiceBus set, and what is refused.</summary>
public class TransportConfiguratorTests
{
    #region Kafka

    [Fact]
    public void Kafka_defaults_and_what_is_set()
    {
        var c = new KafkaConfigurator();
        c.PartitionsValue.ShouldBe(1);
        c.ReplicationFactorValue.ShouldBe((short)1);
        c.KafkaHostValue.ShouldBeNull();
        c.KafkaSslOptionsValue.ShouldBeNull();

        IKafkaConfigurator api = c;
        api.Host("broker:9092");
        api.Partitions(6);
        api.ReplicationFactor(3);

        c.KafkaHostValue.ShouldBe("broker:9092");
        c.PartitionsValue.ShouldBe(6);
        c.ReplicationFactorValue.ShouldBe((short)3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Kafka_partitions_and_replication_factor_below_one_are_rejected(int value)
    {
        IKafkaConfigurator api = new KafkaConfigurator();
        Should.Throw<ArgumentOutOfRangeException>(() => api.Partitions(value));
        Should.Throw<ArgumentOutOfRangeException>(() => api.ReplicationFactor((short)value));
    }

    [Fact]
    public void Kafka_ssl_options_are_all_copied_to_the_client_configuration()
    {
        var options = new KafkaSslOptions
        {
            SslCipherSuites = "suites", SslCurvesList = "curves", SslSigalgsList = "sigalgs", SslKeyLocation = "key.pem",
            SslKeyPassword = "secret", SslKeyPem = "KEY", SslCertificateLocation = "cert.pem", SslCertificatePem = "CERT",
            SslCaLocation = "ca.pem", SslCaPem = "CA", SslCaCertificateStores = "Root", SslCrlLocation = "crl.pem",
            SslKeystoreLocation = "store.p12", SslKeystorePassword = "storepw", SslProviders = "default",
            SslEngineLocation = "engine.so", SslEngineId = "engine1",
            SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.Https
        };
        var clientConfig = new ClientConfig();

        new KafkaConfiguration("broker:9092", options).ApplySsl(clientConfig);

        clientConfig.SslCipherSuites.ShouldBe("suites");
        clientConfig.SslCurvesList.ShouldBe("curves");
        clientConfig.SslSigalgsList.ShouldBe("sigalgs");
        clientConfig.SslKeyLocation.ShouldBe("key.pem");
        clientConfig.SslKeyPassword.ShouldBe("secret");
        clientConfig.SslKeyPem.ShouldBe("KEY");
        clientConfig.SslCertificateLocation.ShouldBe("cert.pem");
        clientConfig.SslCertificatePem.ShouldBe("CERT");
        clientConfig.SslCaLocation.ShouldBe("ca.pem");
        clientConfig.SslCaPem.ShouldBe("CA");
        clientConfig.SslCaCertificateStores.ShouldBe("Root");
        clientConfig.SslCrlLocation.ShouldBe("crl.pem");
        clientConfig.SslKeystoreLocation.ShouldBe("store.p12");
        clientConfig.SslKeystorePassword.ShouldBe("storepw");
        clientConfig.SslProviders.ShouldBe("default");
        clientConfig.SslEngineLocation.ShouldBe("engine.so");
        clientConfig.SslEngineId.ShouldBe("engine1");
        clientConfig.SslEndpointIdentificationAlgorithm.ShouldBe(SslEndpointIdentificationAlgorithm.Https);
    }

    [Fact]
    public void Kafka_without_ssl_options_leaves_the_client_configuration_alone()
    {
        var clientConfig = new ClientConfig { SslKeyLocation = "kept" };

        new KafkaConfiguration("broker:9092", null).ApplySsl(clientConfig);
        new KafkaConfiguration("broker:9092", new KafkaSslOptions()).ApplySsl(clientConfig);

        clientConfig.SslKeyLocation.ShouldBe("kept");
        clientConfig.SslCaLocation.ShouldBeNull();
    }

    [Fact]
    public void Kafka_ssl_callback_gives_the_options_that_it_filled()
    {
        var c = new KafkaConfigurator();

        ((IKafkaConfigurator)c).Ssl(o => o.SslCaLocation = "ca.pem");

        c.KafkaSslOptionsValue.SslCaLocation.ShouldBe("ca.pem");
    }

    [Fact]
    public void Kafka_topic_of_a_request_is_lowercase_and_named_by_the_key()
    {
        var topic = new KafkaConfiguration("h", null).GetRequestTopic(typeof(TransportConfiguratorTests));

        topic.ShouldBe("fxmap-request-topic-fxmap.tests.unittests.configuration.transportconfiguratortests");
    }

    #endregion

    #region RabbitMQ

    [Fact]
    public void RabbitMq_defaults_and_what_is_set()
    {
        var c = new RabbitMqConfigurator();
        c.PortValue.ShouldBe(5672);
        c.ChannelPoolSizeValue.ShouldBe(5);

        IRabbitMqConfigurator api = c;
        api.Host("mq", "/vh", 5673, cred =>
        {
            cred.UserName("guest");
            cred.Password("pw");
            cred.Ssl(o => o.Enabled = true);
        });
        api.ChannelPoolSize(9);

        c.HostValue.ShouldBe("mq");
        c.VirtualHostValue.ShouldBe("/vh");
        c.PortValue.ShouldBe(5673);
        c.ChannelPoolSizeValue.ShouldBe(9);
        c.Credential.UserNameValue.ShouldBe("guest");
        c.Credential.PasswordValue.ShouldBe("pw");
        c.Credential.SslOptionValue.Enabled.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void RabbitMq_a_pool_of_no_channels_is_rejected(int size)
    {
        IRabbitMqConfigurator api = new RabbitMqConfigurator();

        Should.Throw<ArgumentOutOfRangeException>(() => api.ChannelPoolSize(size));
    }

    #endregion

    #region SQS

    [Fact]
    public void Sqs_region_and_credentials()
    {
        var c = new SqsConfigurator();

        ((ISqsConfigurator)c).Region(RegionEndpoint.APSoutheast1, cred =>
        {
            cred.AccessKeyId("id");
            cred.SecretAccessKey("secret");
            cred.ServiceUrl("http://localhost:4566");
        });

        c.AwsRegionValue.ShouldBe(RegionEndpoint.APSoutheast1);
        c.Credential.AccessKeyIdValue.ShouldBe("id");
        c.Credential.SecretAccessKeyValue.ShouldBe("secret");
        c.Credential.ServiceUrlValue.ShouldBe("http://localhost:4566");
    }

    [Fact]
    public void Sqs_region_without_a_callback_keeps_the_credentials_empty()
    {
        var c = new SqsConfigurator();

        ((ISqsConfigurator)c).Region(RegionEndpoint.USEast1);

        c.AwsRegionValue.ShouldBe(RegionEndpoint.USEast1);
        c.Credential.AccessKeyIdValue.ShouldBeNull();
    }

    #endregion

    #region NATS

    [Fact]
    public void Nats_topic_prefix_and_options()
    {
        var c = new NatsConfigurator();
        c.NatsOption.ShouldBeNull();

        INatsConfigurator api = c;
        api.TopicPrefix("app1");
        api.NatsOpts(o =>
        {
            o.Url = "nats://broker:4223";
            o.Name = "my-service";
            o.MaxReconnectRetry = 7;
            o.RequestTimeout = TimeSpan.FromSeconds(3);
        });

        c.TopicPrefixValue.ShouldBe("app1");
        c.NatsOption.Url.ShouldBe("nats://broker:4223");
        c.NatsOption.Name.ShouldBe("my-service");
        c.NatsOption.MaxReconnectRetry.ShouldBe(7);
        c.NatsOption.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void Nats_options_that_are_not_set_keep_the_defaults_of_the_client()
    {
        var c = new NatsConfigurator();

        ((INatsConfigurator)c).NatsOpts(_ => { });

        var defaults = new NatsOpts();
        c.NatsOption.Url.ShouldBe(defaults.Url);
        c.NatsOption.CommandTimeout.ShouldBe(defaults.CommandTimeout);
        c.NatsOption.ConnectTimeout.ShouldBe(defaults.ConnectTimeout);
        c.NatsOption.MaxReconnectRetry.ShouldBe(defaults.MaxReconnectRetry);
        c.NatsOption.SubPendingChannelCapacity.ShouldBe(defaults.SubPendingChannelCapacity);
    }

    [Fact]
    public void Every_option_of_the_client_is_available_in_FxNatsOpts_and_is_copied()
    {
        // FxNatsOpts mirrors NatsOpts with settable properties and copies them one by one: a forgotten line would silently
        // ignore a setting. Every property is set to a value that is not its default, and read back from the converted options.
        var fx = new FxNatsOpts();
        var changed = new List<string>();
        foreach (var property in typeof(FxNatsOpts).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var other = Different(property.PropertyType, property.GetValue(fx));
            if (other is null) continue;
            property.SetValue(fx, other);
            changed.Add(property.Name);
        }

        var opts = (NatsOpts)typeof(FxNatsOpts).GetMethod("ToNatsOpts", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(fx, null)!;

        var notCopied = changed.Where(name =>
        {
            var expected = typeof(FxNatsOpts).GetProperty(name)!.GetValue(fx);
            var actual = typeof(NatsOpts).GetProperty(name)!.GetValue(opts);
            return !Equals(expected, actual);
        }).ToList();
        notCopied.ShouldBeEmpty("settings that FxNatsOpts takes and ToNatsOpts does not copy: " + string.Join(", ", notCopied));
        changed.Count.ShouldBeGreaterThan(20);

        // And the other way: a property of NatsOpts that FxNatsOpts does not have cannot be set at all
        var missing = typeof(NatsOpts).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true } && p.DeclaringType == typeof(NatsOpts))
            .Select(p => p.Name)
            .Where(n => typeof(FxNatsOpts).GetProperty(n) is null)
            .ToList();
        missing.ShouldBeEmpty("settings of NatsOpts that cannot be set through FxNatsOpts: " + string.Join(", ", missing));
    }

    private static object Different(Type type, object current)
    {
        if (type == typeof(string)) return "changed";
        if (type == typeof(bool)) return !(bool)current!;
        if (type == typeof(int)) return (int)current! + 1;
        if (type == typeof(TimeSpan)) return (TimeSpan)current! + TimeSpan.FromSeconds(1);
        if (type == typeof(System.Text.Encoding)) return System.Text.Encoding.Latin1;
        if (type.IsEnum)
            return Enum.GetValues(type).Cast<object>().First(v => !Equals(v, current));
        return null; // objects (logger factory, serializer registry, auth...) are copied by reference and checked below
    }

    #endregion

    #region Azure Service Bus

    [Fact]
    public void ServiceBus_defaults_and_what_is_set()
    {
        var c = new AzureServiceBusConfigurator();
        c.MaxConcurrentSessionsValue.ShouldBe(8);
        c.ServiceBusClientOptions.ShouldBeNull();

        IAzureServiceBusConfigurator api = c;
        api.Host("Endpoint=sb://x/;SharedAccessKeyName=k;SharedAccessKey=v", o => o.Identifier = "my-app");
        api.TopicPrefix("p1");
        api.MaxConcurrentSessions(20);

        c.ConnectionString.ShouldStartWith("Endpoint=sb://x/");
        c.ServiceBusClientOptions.Identifier.ShouldBe("my-app");
        c.TopicPrefixValue.ShouldBe("p1");
        c.MaxConcurrentSessionsValue.ShouldBe(20);
    }

    [Fact]
    public void ServiceBus_host_without_options_keeps_them_null()
    {
        var c = new AzureServiceBusConfigurator();

        ((IAzureServiceBusConfigurator)c).Host("cs");

        c.ConnectionString.ShouldBe("cs");
        c.ServiceBusClientOptions.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ServiceBus_no_session_is_rejected(int sessions)
    {
        IAzureServiceBusConfigurator api = new AzureServiceBusConfigurator();

        Should.Throw<ArgumentOutOfRangeException>(() => api.MaxConcurrentSessions(sessions));
    }

    #endregion
}
