using System.Net;
using SchoolAdmission.Api.Tests.Infrastructure;

namespace SchoolAdmission.Api.Tests;

public class HoldCancelTests(PostgresDatabaseFixture database) : ApiTestBase(database)
{
    private static string Student(int n) => ApiClient.StudentId(n);

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(code, (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    private async Task<string> StatusOfAsync(string holdId) =>
        (await (await Api.GetAsync($"/holds/{holdId}")).ReadJsonAsync()).GetProperty("status").GetString()!;

    [Fact]
    public async Task Owner_cancels_a_held_application_with_204_and_the_seat_is_freed()
    {
        var classId = await Api.CreateClassAsync(seats: 1);
        var holdId = await Api.HoldIdAsync(classId, Student(1));

        var response = await Api.CancelAsync(holdId, Student(1));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        Assert.Equal("CANCELLED", await StatusOfAsync(holdId));
        var cls = await Api.GetClassAsync(classId);
        Assert.Equal(0, cls.GetProperty("heldSeats").GetInt32());
        Assert.Equal(1, cls.GetProperty("availableSeats").GetInt32());

        // The freed seat can be taken by someone else.
        Assert.Equal(HttpStatusCode.Created, (await Api.HoldAsync(classId, Student(2))).StatusCode);
    }

    [Fact]
    public async Task Cancelling_twice_succeeds_both_times()
    {
        var holdId = await Api.HoldIdAsync(await Api.CreateClassAsync(), Student(1));
        await Api.CancelAsync(holdId, Student(1));

        var again = await Api.CancelAsync(holdId, Student(1));

        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Equal("CANCELLED", await StatusOfAsync(holdId));
    }

    [Fact]
    public async Task A_cancelled_application_cannot_be_confirmed()
    {
        var holdId = await Api.HoldIdAsync(await Api.CreateClassAsync(), Student(1));
        await Api.CancelAsync(holdId, Student(1));

        await AssertProblemAsync(await Api.ConfirmAsync(holdId, Student(1)), HttpStatusCode.Conflict, "HOLD_CANCELLED");
    }

    [Fact]
    public async Task After_cancelling_the_student_can_apply_to_the_same_class_again()
    {
        var classId = await Api.CreateClassAsync(seats: 3);
        var first = await Api.HoldIdAsync(classId, Student(601));
        await Api.CancelAsync(first, Student(601));

        var second = await Api.HoldAsync(classId, Student(601));

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var mine = (await (await Api.GetAsync("/me/holds", Student(601))).ReadJsonAsync()).EnumerateArray()
            .Select(i => i.GetProperty("status").GetString()).ToList();
        Assert.Equal(new[] { "HELD", "CANCELLED" }, mine);
    }

    [Fact]
    public async Task Another_student_cannot_cancel_the_application()
    {
        var holdId = await Api.HoldIdAsync(await Api.CreateClassAsync(), Student(1));

        await AssertProblemAsync(await Api.CancelAsync(holdId, Student(2)), HttpStatusCode.Forbidden, "NOT_HOLD_OWNER");

        Assert.Equal("HELD", await StatusOfAsync(holdId));
    }

    [Fact]
    public async Task A_confirmed_admission_cannot_be_cancelled()
    {
        var holdId = await Api.HoldIdAsync(await Api.CreateClassAsync(), Student(1));
        await Api.ConfirmAsync(holdId, Student(1));

        await AssertProblemAsync(
            await Api.CancelAsync(holdId, Student(1)), HttpStatusCode.Conflict, "CANNOT_CANCEL_CONFIRMED");

        Assert.Equal("CONFIRMED", await StatusOfAsync(holdId));
    }

    [Fact]
    public async Task A_hold_past_its_expiry_cannot_be_cancelled_and_stays_expired()
    {
        var classId = await Api.CreateClassAsync();
        var holdId = await Db.InsertHoldAsync(classId, Student(1), "HELD", expiresInSeconds: -5);

        await AssertProblemAsync(await Api.CancelAsync(holdId, Student(1)), HttpStatusCode.Conflict, "HOLD_EXPIRED");

        Assert.Equal("EXPIRED", await StatusOfAsync(holdId));
        Assert.Equal(0L, await Db.CountHoldsAsync(classId, "CANCELLED"));
    }

    [Fact]
    public async Task An_expired_hold_cannot_be_cancelled()
    {
        var holdId = await Db.InsertHoldAsync(await Api.CreateClassAsync(), Student(1), "EXPIRED");

        await AssertProblemAsync(await Api.CancelAsync(holdId, Student(1)), HttpStatusCode.Conflict, "HOLD_EXPIRED");
    }

    [Theory]
    [InlineData("h_00000000000000000000000000000000")]
    [InlineData("h_nonsense")]
    [InlineData("c_0193a5c2e4b87c3f9a1d2e3f4a5b6c7d")]
    public async Task Unknown_or_malformed_hold_returns_404(string holdId) =>
        await AssertProblemAsync(await Api.CancelAsync(holdId, Student(1)), HttpStatusCode.NotFound, "HOLD_NOT_FOUND");

    [Fact]
    public async Task Missing_or_invalid_header_returns_400()
    {
        const string unknown = "h_00000000000000000000000000000000";

        await AssertProblemAsync(await Api.CancelAsync(unknown, null), HttpStatusCode.BadRequest, "STUDENT_ID_REQUIRED");
        await AssertProblemAsync(await Api.CancelAsync(unknown, "1"), HttpStatusCode.BadRequest, "INVALID_STUDENT_ID");
    }

    [Fact]
    public async Task Seats_freed_by_cancelling_go_to_exactly_that_many_racing_students()
    {
        const int seats = 5;
        var classId = await Api.CreateClassAsync(seats: seats);
        var holdIds = new List<string>();
        for (var i = 0; i < seats; i++)
        {
            holdIds.Add(await Api.HoldIdAsync(classId, Student(i)));
        }

        await Api.CancelAsync(holdIds[0], Student(0));
        await Api.CancelAsync(holdIds[1], Student(1));
        var responses = await Concurrent.RunAsync(30, i => Api.HoldAsync(classId, Student(1000 + i)));

        Assert.Equal(2, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(28, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal((long)seats, await Db.CountHoldsAsync(classId, "HELD", "CONFIRMED"));
    }

    [Fact]
    public async Task Cancellations_racing_new_applications_never_push_the_class_over_capacity()
    {
        const int seats = 5;
        var classId = await Api.CreateClassAsync(seats: seats);
        var holdIds = new List<string>();
        for (var i = 0; i < seats; i++)
        {
            holdIds.Add(await Api.HoldIdAsync(classId, Student(i)));
        }

        var results = await Concurrent.RunAsync(32, async i =>
        {
            if (i < 2)
            {
                return (await Api.CancelAsync(holdIds[i], Student(i))).StatusCode;
            }

            return (await Api.HoldAsync(classId, Student(1000 + i))).StatusCode;
        });

        Assert.All(results, code => Assert.True(
            code is HttpStatusCode.NoContent or HttpStatusCode.Created or HttpStatusCode.Conflict));
        var newcomers = results.Skip(2).Count(code => code == HttpStatusCode.Created);
        Assert.InRange(newcomers, 0, 2);
        var active = await Db.CountHoldsAsync(classId, "HELD", "CONFIRMED");
        Assert.Equal(seats - 2 + newcomers, active);
        Assert.True(active <= seats);
    }

    [Fact]
    public async Task Confirm_racing_cancel_on_the_same_application_has_exactly_one_winner()
    {
        const int pairs = 10;
        var classId = await Api.CreateClassAsync(seats: pairs);
        var holdIds = new List<string>();
        for (var i = 0; i < pairs; i++)
        {
            holdIds.Add(await Api.HoldIdAsync(classId, Student(200 + i)));
        }

        // Each application gets a confirm and a cancel released at the same instant.
        var results = await Concurrent.RunAsync(pairs * 2, async i =>
        {
            var pair = i / 2;
            return i % 2 == 0
                ? (await Api.ConfirmAsync(holdIds[pair], Student(200 + pair))).StatusCode
                : (await Api.CancelAsync(holdIds[pair], Student(200 + pair))).StatusCode;
        });

        for (var pair = 0; pair < pairs; pair++)
        {
            var confirm = results[pair * 2];
            var cancel = results[pair * 2 + 1];
            var status = await StatusOfAsync(holdIds[pair]);
            if (status == "CONFIRMED")
            {
                Assert.Equal(HttpStatusCode.OK, confirm);
                Assert.Equal(HttpStatusCode.Conflict, cancel);
            }
            else
            {
                Assert.Equal("CANCELLED", status);
                Assert.Equal(HttpStatusCode.NoContent, cancel);
                Assert.Equal(HttpStatusCode.Conflict, confirm);
            }
        }

        var confirmed = await Db.CountHoldsAsync(classId, "CONFIRMED");
        var cancelled = await Db.CountHoldsAsync(classId, "CANCELLED");
        Assert.Equal(pairs, confirmed + cancelled);
    }
}
