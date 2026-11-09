using System.Net;
using System.Text.Json;
using SchoolAdmission.Api.Tests.Infrastructure;

namespace SchoolAdmission.Api.Tests;

public class HoldCreationTests(PostgresDatabaseFixture database) : ApiTestBase(database)
{
    protected override int HoldSeconds => 300;

    private static string Student(int n) => ApiClient.StudentId(n);

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(code, (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Applying_returns_201_with_the_contract_fields()
    {
        var classId = await Api.CreateClassAsync("Class 6 - Morning Shift", 5);

        var response = await Api.HoldAsync(classId, Student(1));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.ReadJsonAsync();
        var holdId = json.GetProperty("holdId").GetString()!;
        Assert.Matches("^h_[0-9a-f]{32}$", holdId);
        Assert.Equal($"/holds/{holdId}", response.Headers.Location?.OriginalString);
        Assert.Equal(classId, json.GetProperty("classId").GetString());
        Assert.Equal(Student(1), json.GetProperty("studentId").GetString());
        Assert.Equal("HELD", json.GetProperty("status").GetString());
        Assert.Equal("Class 6 - Morning Shift", json.GetProperty("className").GetString());

        var expiresAt = json.GetProperty("expiresAt").GetDateTimeOffset();
        var serverTime = json.GetProperty("serverTime").GetDateTimeOffset();
        Assert.Equal(300, (expiresAt - serverTime).TotalSeconds, precision: 1);
        Assert.EndsWith("Z", json.GetProperty("expiresAt").GetString());
    }

    [Fact]
    public async Task Applying_moves_a_seat_from_available_to_held()
    {
        var classId = await Api.CreateClassAsync("Counts", 3);

        await Api.HoldAsync(classId, Student(1));
        var json = await Api.GetClassAsync(classId);

        Assert.Equal(1, json.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, json.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(2, json.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task Missing_header_returns_400_student_id_required()
    {
        var classId = await Api.CreateClassAsync();

        await AssertProblemAsync(await Api.HoldAsync(classId, null), HttpStatusCode.BadRequest, "STUDENT_ID_REQUIRED");
    }

    [Theory]
    [InlineData("2015269123456789")]      // 16 digits
    [InlineData("201526912345678901")]    // 18 digits
    [InlineData("2015269123456789a")]     // letter
    [InlineData("2015 269123456789")]     // inner space
    [InlineData("২০১৫২৬৯১২৩৪৫৬৭৮৯০")]      // Bangla digits are not accepted by the API
    [InlineData("-2015269123456789")]
    public async Task Malformed_header_returns_400_invalid_student_id(string header)
    {
        var classId = await Api.CreateClassAsync();

        await AssertProblemAsync(await Api.HoldAsync(classId, header), HttpStatusCode.BadRequest, "INVALID_STUDENT_ID");
    }

    [Fact]
    public async Task Header_is_validated_before_the_class_is_looked_up()
    {
        var response = await Api.HoldAsync("c_00000000000000000000000000000000", "bad");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "INVALID_STUDENT_ID");
    }

    [Theory]
    [InlineData("c_00000000000000000000000000000000")]
    [InlineData("c_nonsense")]
    [InlineData("h_0193a5c2e4b87c3f9a1d2e3f4a5b6c7d")]
    public async Task Unknown_or_malformed_class_returns_404(string classId) =>
        await AssertProblemAsync(await Api.HoldAsync(classId, Student(1)), HttpStatusCode.NotFound, "CLASS_NOT_FOUND");

    [Fact]
    public async Task Same_student_applying_twice_in_one_class_returns_409_active_hold_exists()
    {
        var classId = await Api.CreateClassAsync(seats: 5);
        await Api.HoldAsync(classId, Student(1));

        var second = await Api.HoldAsync(classId, Student(1));

        await AssertProblemAsync(second, HttpStatusCode.Conflict, "ACTIVE_HOLD_EXISTS");
        Assert.Equal(1L, await Db.CountHoldsAsync(classId, "HELD"));
    }

    [Fact]
    public async Task Student_with_a_confirmed_admission_gets_409_already_confirmed()
    {
        var classId = await Api.CreateClassAsync(seats: 5);
        await Db.InsertHoldAsync(classId, Student(1), "CONFIRMED");

        await AssertProblemAsync(await Api.HoldAsync(classId, Student(1)), HttpStatusCode.Conflict, "ALREADY_CONFIRMED");
    }

    [Fact]
    public async Task Full_class_returns_409_no_seats_available()
    {
        var classId = await Api.CreateClassAsync(seats: 2);
        Assert.Equal(HttpStatusCode.Created, (await Api.HoldAsync(classId, Student(1))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Api.HoldAsync(classId, Student(2))).StatusCode);

        await AssertProblemAsync(await Api.HoldAsync(classId, Student(3)), HttpStatusCode.Conflict, "NO_SEATS_AVAILABLE");

        var json = await Api.GetClassAsync(classId);
        Assert.Equal(2, json.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, json.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task Confirmed_seats_count_against_capacity()
    {
        var classId = await Api.CreateClassAsync(seats: 2);
        await Db.InsertHoldAsync(classId, Student(1), "CONFIRMED");
        await Db.InsertHoldAsync(classId, Student(2), "CONFIRMED");

        await AssertProblemAsync(await Api.HoldAsync(classId, Student(3)), HttpStatusCode.Conflict, "NO_SEATS_AVAILABLE");
    }

    [Fact]
    public async Task An_expired_hold_frees_its_seat_even_though_its_row_still_says_HELD()
    {
        var classId = await Api.CreateClassAsync(seats: 1);
        await Db.InsertHoldAsync(classId, Student(1), "HELD", expiresInSeconds: -5);

        var response = await Api.HoldAsync(classId, Student(2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_student_whose_hold_expired_can_apply_again_to_the_same_class()
    {
        var classId = await Api.CreateClassAsync(seats: 3);
        await Db.InsertHoldAsync(classId, Student(1), "HELD", expiresInSeconds: -5);

        var response = await Api.HoldAsync(classId, Student(1));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1L, await Db.CountHoldsAsync(classId, "EXPIRED"));
        Assert.Equal(1L, await Db.CountHoldsAsync(classId, "HELD"));
    }

    [Fact]
    public async Task A_student_may_hold_seats_in_different_classes_at_the_same_time()
    {
        var morning = await Api.CreateClassAsync("Class 6 - Morning Shift", 2);
        var day = await Api.CreateClassAsync("Class 6 - Day Shift", 2);

        Assert.Equal(HttpStatusCode.Created, (await Api.HoldAsync(morning, Student(1))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Api.HoldAsync(day, Student(1))).StatusCode);
    }

    [Fact]
    public async Task Full_class_does_not_block_other_classes()
    {
        var full = await Api.CreateClassAsync("Full", 1);
        var open = await Api.CreateClassAsync("Open", 1);
        await Api.HoldAsync(full, Student(1));

        Assert.Equal(HttpStatusCode.Created, (await Api.HoldAsync(open, Student(2))).StatusCode);
    }

    [Fact]
    public async Task Request_body_is_ignored()
    {
        var classId = await Api.CreateClassAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds")
        {
            Content = new StringContent("not json at all"),
        };
        request.Headers.Add("X-Student-Id", Student(1));

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
