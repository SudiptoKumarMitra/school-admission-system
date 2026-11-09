using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using SchoolAdmission.Api.Tests.Infrastructure;

namespace SchoolAdmission.Api.Tests;

public class ClassesApiTests(PostgresDatabaseFixture database) : ApiTestBase(database)
{
    [Fact]
    public async Task Create_returns_201_with_location_and_the_contract_fields()
    {
        var response = await Client.PostAsJsonAsync("/classes", new { name = "Class 6 - Morning Shift", totalSeats = 120 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.ReadJsonAsync();
        var id = json.GetProperty("id").GetString()!;
        Assert.Matches("^c_[0-9a-f]{32}$", id);
        Assert.Equal($"/classes/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal("Class 6 - Morning Shift", json.GetProperty("name").GetString());
        Assert.Equal(120, json.GetProperty("totalSeats").GetInt32());
        Assert.Equal(0, json.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, json.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(120, json.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task Create_trims_the_name()
    {
        var response = await Client.PostAsJsonAsync("/classes", new { name = "  Class 7  ", totalSeats = 5 });

        Assert.Equal("Class 7", (await response.ReadJsonAsync()).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Duplicate_names_create_separate_classes()
    {
        var first = await Api.CreateClassAsync("Class 8 - Day Shift", 3);
        var second = await Api.CreateClassAsync("Class 8 - Day Shift", 3);

        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData("""{"totalSeats":10}""")]
    [InlineData("""{"name":"   ","totalSeats":10}""")]
    [InlineData("""{"name":null,"totalSeats":10}""")]
    [InlineData("""{"name":123,"totalSeats":10}""")]
    [InlineData("""{"name":"Class","totalSeats":0}""")]
    [InlineData("""{"name":"Class","totalSeats":-4}""")]
    [InlineData("""{"name":"Class","totalSeats":100001}""")]
    [InlineData("""{"name":"Class","totalSeats":"120"}""")]
    [InlineData("""{"name":"Class","totalSeats":12.5}""")]
    [InlineData("""{"name":"Class","totalSeats":null}""")]
    [InlineData("""{"name":"Class"}""")]
    [InlineData("[]")]
    [InlineData("42")]
    public async Task Create_rejects_invalid_input_with_problem_json(string body)
    {
        var response = await Api.PostRawAsync("/classes", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.ReadJsonAsync();
        Assert.Equal("VALIDATION_ERROR", json.GetProperty("code").GetString());
        Assert.Equal(400, json.GetProperty("status").GetInt32());
        Assert.True(json.GetProperty("errors").EnumerateObject().Any());
    }

    [Fact]
    public async Task Create_rejects_a_name_longer_than_100_characters()
    {
        var response = await Client.PostAsJsonAsync("/classes", new { name = new string('x', 101), totalSeats = 10 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.ReadJsonAsync()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("")]
    public async Task Create_rejects_malformed_or_empty_body_with_problem_json(string body)
    {
        var response = await Api.PostRawAsync("/classes", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task List_contains_created_classes_oldest_first_with_the_contract_fields()
    {
        var first = await Api.CreateClassAsync("List A", 4);
        var second = await Api.CreateClassAsync("List B", 6);

        var response = await Client.GetAsync("/classes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.ReadJsonAsync()).EnumerateArray().ToList();
        var ids = items.Select(i => i.GetProperty("id").GetString()).ToList();
        Assert.True(ids.IndexOf(first) >= 0 && ids.IndexOf(first) < ids.IndexOf(second));

        var item = items.Single(i => i.GetProperty("id").GetString() == second);
        foreach (var field in new[] { "id", "name", "totalSeats", "heldSeats", "confirmedSeats", "availableSeats" })
        {
            Assert.True(item.TryGetProperty(field, out _), $"missing field {field}");
        }

        Assert.Equal(6, item.GetProperty("availableSeats").GetInt32());
        Assert.Matches(new Regex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z$"),
            item.GetProperty("createdAt").GetString()!);
    }

    [Fact]
    public async Task Get_returns_one_class()
    {
        var id = await Api.CreateClassAsync("Single", 9);

        var response = await Client.GetAsync($"/classes/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal(id, json.GetProperty("id").GetString());
        Assert.Equal(9, json.GetProperty("totalSeats").GetInt32());
        Assert.Equal(9, json.GetProperty("availableSeats").GetInt32());
    }

    [Theory]
    [InlineData("c_00000000000000000000000000000000")]
    [InlineData("c_0193a5c2e4b87c3f9a1d2e3f4a5b6c7d")]
    [InlineData("h_0193a5c2e4b87c3f9a1d2e3f4a5b6c7d")]
    [InlineData("c_xyz")]
    [InlineData("123")]
    public async Task Get_unknown_or_malformed_id_returns_404_problem_json(string id)
    {
        var response = await Client.GetAsync($"/classes/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("CLASS_NOT_FOUND", (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Seat_counts_reflect_holds_stored_in_the_database()
    {
        var id = await Api.CreateClassAsync("Counting", 10);
        var classGuid = Guid.ParseExact(id[2..], "N");
        await using (var connection = new Npgsql.NpgsqlConnection(Database.ConnectionString))
        {
            await connection.OpenAsync();
            // 2 live holds, 1 expired-but-still-marked-HELD hold, 3 confirmed, 1 cancelled.
            var rows = new (string Status, string Expires)[]
            {
                ("HELD", "10 minutes"), ("HELD", "10 minutes"), ("HELD", "-1 minutes"),
                ("CONFIRMED", "10 minutes"), ("CONFIRMED", "10 minutes"), ("CONFIRMED", "10 minutes"),
                ("CANCELLED", "10 minutes"),
            };
            for (var i = 0; i < rows.Length; i++)
            {
                await using var command = new Npgsql.NpgsqlCommand(
                    "INSERT INTO holds (id, class_id, student_id, status, expires_at) " +
                    $"VALUES (@id, @c, @s, @st, now() + interval '{rows[i].Expires}')", connection);
                command.Parameters.AddWithValue("id", Guid.CreateVersion7());
                command.Parameters.AddWithValue("c", classGuid);
                command.Parameters.AddWithValue("s", ApiClient.StudentId(i));
                command.Parameters.AddWithValue("st", rows[i].Status);
                await command.ExecuteNonQueryAsync();
            }
        }

        var json = await Api.GetClassAsync(id);

        Assert.Equal(2, json.GetProperty("heldSeats").GetInt32());
        Assert.Equal(3, json.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(5, json.GetProperty("availableSeats").GetInt32());
    }
}
