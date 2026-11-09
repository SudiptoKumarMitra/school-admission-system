using System.Net;
using SchoolAdmission.Api.Tests.Infrastructure;

namespace SchoolAdmission.Api.Tests;

/// <summary>
/// Separate API hosts (own DI container, own connection pool) sharing one database, standing in for
/// two backend processes on different ports. Seat limits must hold because the lock lives in PostgreSQL.
/// </summary>
public class MultiInstanceConcurrencyTests(PostgresDatabaseFixture database) : IClassFixture<PostgresDatabaseFixture>
{
    private static string Student(int n) => ApiClient.StudentId(n);

    private sealed class Hosts : IAsyncDisposable
    {
        private readonly List<ApiFactory> _factories = [];

        public List<ApiClient> Clients { get; } = [];

        public Hosts(string connectionString, int count, int holdSeconds = 600, int sweepSeconds = 1)
        {
            for (var i = 0; i < count; i++)
            {
                var factory = new ApiFactory(connectionString, holdSeconds, sweepSeconds);
                _factories.Add(factory);
                Clients.Add(new ApiClient(factory.CreateClient()));
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var factory in _factories)
            {
                await factory.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task Two_backend_processes_never_hold_more_seats_than_exist()
    {
        const int seats = 20;
        const int students = 150;
        await using var hosts = new Hosts(database.ConnectionString, 2);
        var classId = await hosts.Clients[0].CreateClassAsync("Two processes", seats);

        // Even-numbered students call process 0, odd-numbered students call process 1.
        var responses = await Concurrent.RunAsync(students, i => hosts.Clients[i % 2].HoldAsync(classId, Student(i)));

        Assert.Equal(seats, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(students - seats, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        foreach (var client in hosts.Clients)
        {
            var cls = await client.GetClassAsync(classId);
            Assert.Equal(seats, cls.GetProperty("heldSeats").GetInt32());
            Assert.Equal(0, cls.GetProperty("availableSeats").GetInt32());
        }

        var test = new TestDb(database.ConnectionString);
        Assert.Equal((long)seats, await test.CountHoldsAsync(classId, "HELD", "CONFIRMED"));
    }

    [Fact]
    public async Task Three_processes_racing_over_several_classes_each_respect_every_capacity()
    {
        const int classes = 4;
        const int seats = 5;
        await using var hosts = new Hosts(database.ConnectionString, 3);
        var classIds = new List<string>();
        for (var c = 0; c < classes; c++)
        {
            classIds.Add(await hosts.Clients[0].CreateClassAsync($"Multi {c}", seats));
        }

        var responses = await Concurrent.RunAsync(classes * 60, i =>
            hosts.Clients[i % 3].HoldAsync(classIds[i % classes], Student(2000 + i)));

        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict));
        var test = new TestDb(database.ConnectionString);
        foreach (var classId in classIds)
        {
            Assert.Equal((long)seats, await test.CountHoldsAsync(classId, "HELD", "CONFIRMED"));
        }
    }

    [Fact]
    public async Task One_student_applying_through_different_processes_at_once_gets_one_application()
    {
        await using var hosts = new Hosts(database.ConnectionString, 3);
        var classId = await hosts.Clients[0].CreateClassAsync("Same student", 50);

        var responses = await Concurrent.RunAsync(30, i => hosts.Clients[i % 3].HoldAsync(classId, Student(7)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(29, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Confirming_the_same_hold_through_different_processes_counts_it_once()
    {
        await using var hosts = new Hosts(database.ConnectionString, 2);
        var classId = await hosts.Clients[0].CreateClassAsync("Confirm twice", 5);
        var holdId = await hosts.Clients[0].HoldIdAsync(classId, Student(9));

        var responses = await Concurrent.RunAsync(20, i => hosts.Clients[i % 2].ConfirmAsync(holdId, Student(9)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var cls = await hosts.Clients[1].GetClassAsync(classId);
        Assert.Equal(1, cls.GetProperty("confirmedSeats").GetInt32());
    }

    [Fact]
    public async Task Data_survives_a_backend_restart()
    {
        string classId, confirmedHold, heldHold;
        await using (var first = new Hosts(database.ConnectionString, 1))
        {
            var api = first.Clients[0];
            classId = await api.CreateClassAsync("Persistent", 5);
            confirmedHold = await api.HoldIdAsync(classId, Student(31));
            heldHold = await api.HoldIdAsync(classId, Student(32));
            await api.ConfirmAsync(confirmedHold, Student(31));
        } // the first backend is gone

        await using var second = new Hosts(database.ConnectionString, 1);
        var restarted = second.Clients[0];

        var cls = await restarted.GetClassAsync(classId);
        Assert.Equal("Persistent", cls.GetProperty("name").GetString());
        Assert.Equal(1, cls.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(1, cls.GetProperty("heldSeats").GetInt32());
        var mine = await (await restarted.GetAsync("/me/holds", Student(31))).ReadJsonAsync();
        Assert.Equal(confirmedHold, mine[0].GetProperty("holdId").GetString());
        Assert.Equal("CONFIRMED", mine[0].GetProperty("status").GetString());
        var held = await (await restarted.GetAsync($"/holds/{heldHold}")).ReadJsonAsync();
        Assert.Equal("HELD", held.GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_hold_that_ran_out_while_the_backend_was_down_reads_as_expired_after_restart()
    {
        string classId, holdId;
        await using (var first = new Hosts(database.ConnectionString, 1, holdSeconds: 2))
        {
            classId = await first.Clients[0].CreateClassAsync("Down time", 1);
            holdId = await first.Clients[0].HoldIdAsync(classId, Student(41));
        }

        await Task.Delay(TimeSpan.FromSeconds(3.5)); // nothing is running

        await using var second = new Hosts(database.ConnectionString, 1, holdSeconds: 2);
        var restarted = second.Clients[0];
        var hold = await (await restarted.GetAsync($"/holds/{holdId}")).ReadJsonAsync();
        Assert.Equal("EXPIRED", hold.GetProperty("status").GetString());
        Assert.Equal(1, (await restarted.GetClassAsync(classId)).GetProperty("availableSeats").GetInt32());
    }
}
