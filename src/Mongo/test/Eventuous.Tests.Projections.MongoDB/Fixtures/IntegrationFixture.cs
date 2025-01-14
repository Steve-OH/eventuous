using System.Text.Json;
using EventStore.Client;
using Eventuous.EventStore;
using Eventuous.TestHelpers;
using MongoDb.Bson.NodaTime;
using MongoDB.Driver;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Testcontainers.EventStoreDb;
using Testcontainers.MongoDb;

namespace Eventuous.Tests.Projections.MongoDB.Fixtures;

public sealed class IntegrationFixture : IAsyncLifetime {
    public IEventStore      EventStore { get; set; }         = null!;
    public EventStoreClient Client     { get; private set; } = null!;
    public IMongoDatabase   Mongo      { get; private set; } = null!;
    public Fixture          Auto       { get; }              = new();

    static IEventSerializer Serializer { get; } = new DefaultEventSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
            .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
    );

    public Task<AppendEventsResult> AppendEvent(StreamName streamName, object evt, ExpectedStreamVersion? version = null)
        => EventStore.AppendEvents(
            streamName,
            version ?? ExpectedStreamVersion.Any,
            new[] { new StreamEvent(Guid.NewGuid(), evt, new Metadata(), "application/json", 0) },
            CancellationToken.None
        );

    static IntegrationFixture() {
        DefaultEventSerializer.SetDefaultSerializer(Serializer);
        NodaTimeSerializers.Register();
    }

    EventStoreDbContainer _esdbContainer  = null!;
    MongoDbContainer      _mongoContainer = null!;

    public async Task InitializeAsync() {
        _esdbContainer = new EventStoreDbContainerBuilder().Build();
        await _esdbContainer.StartAsync();
        var settings = EventStoreClientSettings.Create(_esdbContainer.GetConnectionString());
        Client         = new EventStoreClient(settings);
        EventStore     = new EsdbEventStore(Client);
        new AggregateStore(EventStore);

        _mongoContainer = new MongoDbBuilder().Build();
        await _mongoContainer.StartAsync();
        var mongoSettings = MongoClientSettings.FromConnectionString(_mongoContainer.GetConnectionString());
        Mongo = new MongoClient(mongoSettings).GetDatabase("bookings");
    }

    public async Task DisposeAsync() {
        await Client.DisposeAsync();
        await _esdbContainer.DisposeAsync();
    }
}
