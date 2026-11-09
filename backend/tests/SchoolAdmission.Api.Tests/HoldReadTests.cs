using System.Net;
using System.Text.Json;
using SchoolAdmission.Api.Tests.Infrastructure;

namespace SchoolAdmission.Api.Tests;

public class HoldReadTests(PostgresDatabaseFixture database) : ApiTestBase(database)
{
    // Tests in this class share one database, so tests that list a student's applications use
    // dedicated student numbers (501+) that no other test touches.
    private static string Student(int n) => ApiClient.StudentId(n);

    [Fact]
    public async Task Get_hold_returns_the_contract_fields_and_hides_the_student()
    {
        var classId = await Api.CreateClassAsync("Class 6 - Morning Shift", 5);
        var holdId = await Api.HoldIdAsync(classId, Student(1));

        var response = await Api.GetAsync($"/holds/{holdId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal(holdId, json.GetProperty("holdId").GetString());
        Assert.Equal(classId, json.GetProperty("classId").GetString());
        Assert.Equal("HELD", json.GetProperty("status").GetString());
        Assert.Equal("Class 6 - Morning Shift", json.GetProperty("className").GetString());
        Assert.EndsWith("Z", json.GetProperty("expiresAt").GetString());
        Assert.True(json.TryGetProperty("serverTime", out _));
        Assert.False(json.TryGetProperty("studentId", out _), "GET /holds/{id} has no identity check and must not reveal the student");
    }

    [Fact]
    public async Task Get_hold_works_without_any_header()
    {
        var holdId = await Api.HoldIdAsync(await Api.CreateClassAsync(), Student(1));

        var response = await Client.GetAsync($"/holds/{holdId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("h_00000000000000000000000000000000")]
    [InlineData("h_zzz")]
    [InlineData("c_0193a5c2e4b87c3f9a1d2e3f4a5b6c7d")]
    [InlineData("42")]
    public async Task Get_unknown_or_malformed_hold_returns_404(string holdId)
    {
        var response = await Client.GetAsync($"/holds/{holdId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("HOLD_NOT_FOUND", (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_hold_past_its_expiry_reads_as_EXPIRED_even_if_the_row_still_says_HELD()
    {
        var classId = await Api.CreateClassAsync();
        var holdId = await Db.InsertHoldAsync(classId, Student(1), "HELD", expiresInSeconds: -5);

        var json = await (await Api.GetAsync($"/holds/{holdId}")).ReadJsonAsync();

        Assert.Equal("EXPIRED", json.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("CONFIRMED")]
    [InlineData("CANCELLED")]
    [InlineData("EXPIRED")]
    public async Task Stored_final_statuses_are_reported_as_they_are(string status)
    {
        var classId = await Api.CreateClassAsync();
        var holdId = await Db.InsertHoldAsync(classId, Student(1), status);

        var json = await (await Api.GetAsync($"/holds/{holdId}")).ReadJsonAsync();

        Assert.Equal(status, json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task My_holds_lists_only_this_students_applications_newest_first()
    {
        var a = await Api.CreateClassAsync("Class A", 5);
        var b = await Api.CreateClassAsync("Class B", 5);
        var c = await Api.CreateClassAsync("Class C", 5);
        var first = await Api.HoldIdAsync(a, Student(501));
        var second = await Api.HoldIdAsync(b, Student(501));
        var third = await Api.HoldIdAsync(c, Student(501));
        await Api.HoldIdAsync(a, Student(502)); // someone else's

        var response = await Api.GetAsync("/me/holds", Student(501));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.ReadJsonAsync()).EnumerateArray().ToList();
        Assert.Equal(new[] { third, second, first }, items.Select(i => i.GetProperty("holdId").GetString()).ToArray());
        Assert.Equal(new[] { "Class C", "Class B", "Class A" }, items.Select(i => i.GetProperty("className").GetString()).ToArray());
        foreach (var field in new[] { "holdId", "classId", "className", "status", "expiresAt" })
        {
            Assert.True(items[0].TryGetProperty(field, out _), $"missing field {field}");
        }
    }

    [Fact]
    public async Task My_holds_includes_every_status_with_effective_expiry()
    {
        var classes = new[]
        {
            await Api.CreateClassAsync("K1"), await Api.CreateClassAsync("K2"),
            await Api.CreateClassAsync("K3"), await Api.CreateClassAsync("K4"),
        };
        await Db.InsertHoldAsync(classes[0], Student(503), "HELD");
        await Db.InsertHoldAsync(classes[1], Student(503), "HELD", expiresInSeconds: -5);
        await Db.InsertHoldAsync(classes[2], Student(503), "CONFIRMED");
        await Db.InsertHoldAsync(classes[3], Student(503), "CANCELLED");

        var json = await (await Api.GetAsync("/me/holds", Student(503))).ReadJsonAsync();

        var byClass = json.EnumerateArray().ToDictionary(
            i => i.GetProperty("className").GetString()!, i => i.GetProperty("status").GetString());
        Assert.Equal("HELD", byClass["K1"]);
        Assert.Equal("EXPIRED", byClass["K2"]);
        Assert.Equal("CONFIRMED", byClass["K3"]);
        Assert.Equal("CANCELLED", byClass["K4"]);
    }

    [Fact]
    public async Task My_holds_is_an_empty_array_for_a_student_with_no_applications()
    {
        var response = await Api.GetAsync("/me/holds", Student(99));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Array, (await response.ReadJsonAsync()).ValueKind);
        Assert.Empty((await response.ReadJsonAsync()).EnumerateArray());
    }

    [Fact]
    public async Task My_holds_requires_a_valid_student_header()
    {
        var missing = await Api.GetAsync("/me/holds");
        var invalid = await Api.GetAsync("/me/holds", "123");

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal("STUDENT_ID_REQUIRED", (await missing.ReadJsonAsync()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("INVALID_STUDENT_ID", (await invalid.ReadJsonAsync()).GetProperty("code").GetString());
    }
}
