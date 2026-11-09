using System.Net;
using SchoolAdmission.Api.Tests.Infrastructure;

namespace SchoolAdmission.Api.Tests;

public class HoldConfirmTests(PostgresDatabaseFixture database) : ApiTestBase(database)
{
    private static string Student(int n) => ApiClient.StudentId(n);

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(code, (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Owner_confirms_a_held_application_and_the_seat_moves_to_confirmed()
    {
        var classId = await Api.CreateClassAsync("Class 6 - Morning Shift", 3);
        var holdId = await Api.HoldIdAsync(classId, Student(1));

        var response = await Api.ConfirmAsync(holdId, Student(1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.Equal(holdId, json.GetProperty("holdId").GetString());
        Assert.Equal("CONFIRMED", json.GetProperty("status").GetString());

        var hold = await (await Api.GetAsync($"/holds/{holdId}")).ReadJsonAsync();
        Assert.Equal("CONFIRMED", hold.GetProperty("status").GetString());
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, hold.GetProperty("confirmedAt").ValueKind);

        var cls = await Api.GetClassAsync(classId);
        Assert.Equal(0, cls.GetProperty("heldSeats").GetInt32());
        Assert.Equal(1, cls.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(2, cls.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task Confirming_again_succeeds_and_confirmed_seats_increase_only_once()
    {
        var classId = await Api.CreateClassAsync(seats: 3);
        var holdId = await Api.HoldIdAsync(classId, Student(1));
        var first = await (await Api.ConfirmAsync(holdId, Student(1))).ReadJsonAsync();

        var secondResponse = await Api.ConfirmAsync(holdId, Student(1));
        var third = await Api.ConfirmAsync(holdId, Student(1));

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
        var second = await secondResponse.ReadJsonAsync();
        Assert.Equal("CONFIRMED", second.GetProperty("status").GetString());
        Assert.Equal(first.GetProperty("confirmedAt").GetString(), second.GetProperty("confirmedAt").GetString());
        Assert.Equal(1, (await Api.GetClassAsync(classId)).GetProperty("confirmedSeats").GetInt32());
    }

    [Fact]
    public async Task Another_student_cannot_confirm_the_application()
    {
        var holdId = await Api.HoldIdAsync(await Api.CreateClassAsync(), Student(1));

        await AssertProblemAsync(await Api.ConfirmAsync(holdId, Student(2)), HttpStatusCode.Forbidden, "NOT_HOLD_OWNER");

        var hold = await (await Api.GetAsync($"/holds/{holdId}")).ReadJsonAsync();
        Assert.Equal("HELD", hold.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Another_student_cannot_confirm_even_a_confirmed_application()
    {
        var holdId = await Api.HoldIdAsync(await Api.CreateClassAsync(), Student(1));
        await Api.ConfirmAsync(holdId, Student(1));

        await AssertProblemAsync(await Api.ConfirmAsync(holdId, Student(2)), HttpStatusCode.Forbidden, "NOT_HOLD_OWNER");
    }

    [Theory]
    [InlineData("h_00000000000000000000000000000000")]
    [InlineData("h_nonsense")]
    [InlineData("c_0193a5c2e4b87c3f9a1d2e3f4a5b6c7d")]
    public async Task Unknown_or_malformed_hold_returns_404(string holdId) =>
        await AssertProblemAsync(await Api.ConfirmAsync(holdId, Student(1)), HttpStatusCode.NotFound, "HOLD_NOT_FOUND");

    [Fact]
    public async Task Missing_or_invalid_header_returns_400_before_the_hold_is_looked_up()
    {
        const string unknown = "h_00000000000000000000000000000000";

        await AssertProblemAsync(await Api.ConfirmAsync(unknown, null), HttpStatusCode.BadRequest, "STUDENT_ID_REQUIRED");
        await AssertProblemAsync(await Api.ConfirmAsync(unknown, "12345"), HttpStatusCode.BadRequest, "INVALID_STUDENT_ID");
    }

    [Fact]
    public async Task A_hold_past_its_expiry_cannot_be_confirmed_even_if_its_row_still_says_HELD()
    {
        var classId = await Api.CreateClassAsync();
        var holdId = await Db.InsertHoldAsync(classId, Student(1), "HELD", expiresInSeconds: -5);

        await AssertProblemAsync(await Api.ConfirmAsync(holdId, Student(1)), HttpStatusCode.Conflict, "HOLD_EXPIRED");

        Assert.Equal(0L, await Db.CountHoldsAsync(classId, "CONFIRMED"));
    }

    [Fact]
    public async Task An_expired_hold_cannot_be_confirmed()
    {
        var holdId = await Db.InsertHoldAsync(await Api.CreateClassAsync(), Student(1), "EXPIRED");

        await AssertProblemAsync(await Api.ConfirmAsync(holdId, Student(1)), HttpStatusCode.Conflict, "HOLD_EXPIRED");
    }

    [Fact]
    public async Task A_cancelled_hold_cannot_be_confirmed()
    {
        var holdId = await Db.InsertHoldAsync(await Api.CreateClassAsync(), Student(1), "CANCELLED");

        await AssertProblemAsync(await Api.ConfirmAsync(holdId, Student(1)), HttpStatusCode.Conflict, "HOLD_CANCELLED");
    }

    [Fact]
    public async Task Confirming_does_not_free_or_take_extra_seats()
    {
        var classId = await Api.CreateClassAsync(seats: 1);
        var holdId = await Api.HoldIdAsync(classId, Student(1));
        await Api.ConfirmAsync(holdId, Student(1));

        await AssertProblemAsync(await Api.HoldAsync(classId, Student(2)), HttpStatusCode.Conflict, "NO_SEATS_AVAILABLE");
        await AssertProblemAsync(await Api.HoldAsync(classId, Student(1)), HttpStatusCode.Conflict, "ALREADY_CONFIRMED");
    }

    [Fact]
    public async Task Twenty_simultaneous_confirms_of_one_hold_confirm_it_exactly_once()
    {
        var classId = await Api.CreateClassAsync(seats: 5);
        var holdId = await Api.HoldIdAsync(classId, Student(1));

        var responses = await Concurrent.RunAsync(20, _ => Api.ConfirmAsync(holdId, Student(1)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var confirmedAts = new HashSet<string?>();
        foreach (var response in responses)
        {
            var json = await response.ReadJsonAsync();
            Assert.Equal("CONFIRMED", json.GetProperty("status").GetString());
            confirmedAts.Add(json.GetProperty("confirmedAt").GetString());
        }

        Assert.Single(confirmedAts); // every caller saw the same single confirmation
        Assert.Equal(1, (await Api.GetClassAsync(classId)).GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(1L, await Db.CountHoldsAsync(classId, "CONFIRMED"));
    }

    [Fact]
    public async Task Many_students_confirming_at_once_each_get_their_own_seat()
    {
        const int students = 10;
        var classId = await Api.CreateClassAsync(seats: students);
        var holdIds = new List<string>();
        for (var i = 0; i < students; i++)
        {
            holdIds.Add(await Api.HoldIdAsync(classId, Student(100 + i)));
        }

        var responses = await Concurrent.RunAsync(students, i => Api.ConfirmAsync(holdIds[i], Student(100 + i)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var cls = await Api.GetClassAsync(classId);
        Assert.Equal(students, cls.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(0, cls.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, cls.GetProperty("availableSeats").GetInt32());
    }
}
