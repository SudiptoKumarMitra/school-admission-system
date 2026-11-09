namespace SchoolAdmission.Api.Tests.Infrastructure;

/// <summary>Base class: one isolated database per test class, one API host per test.</summary>
public abstract class ApiTestBase(PostgresDatabaseFixture database)
    : IClassFixture<PostgresDatabaseFixture>, IAsyncLifetime
{
    protected PostgresDatabaseFixture Database { get; } = database;
    protected ApiFactory Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;
    protected ApiClient Api { get; private set; } = null!;
    protected TestDb Db => new(Database.ConnectionString);

    protected virtual int HoldSeconds => 600;

    public Task InitializeAsync()
    {
        Factory = new ApiFactory(Database.ConnectionString, HoldSeconds);
        Client = Factory.CreateClient();
        Api = new ApiClient(Client);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }
}
