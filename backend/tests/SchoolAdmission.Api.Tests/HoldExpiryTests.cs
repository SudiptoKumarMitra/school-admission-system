using System.Net;
using SchoolAdmission.Api.Tests.Infrastructure;

namespace SchoolAdmission.Api.Tests;

/// <summary>Holds last two seconds here, so expiry can be observed for real.</summary>
public class HoldExpiryTests(PostgresDatabaseFixture database) : ApiTestBase(database)
{
    protected override int HoldSeconds => 2;

    private static string Student(int n) => ApiClient.StudentId(n);

    private async Task<string> StoredStatusAsync(string holdId) =>
        await Db.ScalarAsync<string>("SELECT status FROM holds WHERE id = @id", ("id", Guid.ParseExact(holdId[2..], "N")));

    private async Task WaitUntilAsync(Func<Task<bool>> condition, int timeoutSeconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(200);
        }

        Assert.Fail($"Condition was not met within {timeoutSeconds}s");
    }

    [Fact]
    public async Task A_seat_comes_back_by_itself_when_the_hold_expires_with_no_api_call_in_between()
    {
        var classId = await Api.CreateClassAsync("Expiry", 1);
        var holdId = await Api.HoldIdAsync(classId, Student(1));
        Assert.Equal(1, (await Api.GetClassAsync(classId)).GetProperty("heldSeats").GetInt32());

        await Task.Delay(TimeSpan.FromSeconds(3.5)); // nobody calls the API while the hold runs out

        var hold = await (await Api.GetAsync($"/holds/{holdId}")).ReadJsonAsync();
        Assert.Equal("EXPIRED", hold.GetProperty("status").GetString());
        var cls = await Api.GetClassAsync(classId);
        Assert.Equal(0, cls.GetProperty("heldSeats").GetInt32());
        Assert.Equal(1, cls.GetProperty("availableSeats").GetInt32());
        Assert.Equal("EXPIRED", await StoredStatusAsync(holdId)); // the sweeper tidied the row

        var confirm = await Api.ConfirmAsync(holdId, Student(1));
        Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);
        Assert.Equal("HOLD_EXPIRED", (await confirm.ReadJsonAsync()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await Api.CancelAsync(holdId, Student(1))).StatusCode);

        // Someone else can now take the freed seat.
        Assert.Equal(HttpStatusCode.Created, (await Api.HoldAsync(classId, Student(2))).StatusCode);
    }

    [Fact]
    public async Task A_student_can_apply_again_after_their_hold_expired()
    {
        var classId = await Api.CreateClassAsync("Reapply", 3);
        var first = await Api.HoldIdAsync(classId, Student(5));

        await Task.Delay(TimeSpan.FromSeconds(3.5));
        var second = await Api.HoldAsync(classId, Student(5));

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal("EXPIRED", await StoredStatusAsync(first));
    }

    [Fact]
    public async Task The_sweeper_marks_expired_holds_and_leaves_every_other_hold_alone()
    {
        var classId = await Api.CreateClassAsync("Sweep", 10);
        var expired = await Db.InsertHoldAsync(classId, Student(10), "HELD", expiresInSeconds: -30);
        var live = await Db.InsertHoldAsync(classId, Student(11), "HELD", expiresInSeconds: 3600);
        var confirmed = await Db.InsertHoldAsync(classId, Student(12), "CONFIRMED", expiresInSeconds: -30);
        var cancelled = await Db.InsertHoldAsync(classId, Student(13), "CANCELLED", expiresInSeconds: -30);

        await WaitUntilAsync(async () => await StoredStatusAsync(expired) == "EXPIRED");

        Assert.Equal("HELD", await StoredStatusAsync(live));
        Assert.Equal("CONFIRMED", await StoredStatusAsync(confirmed));
        Assert.Equal("CANCELLED", await StoredStatusAsync(cancelled));
        var expiredAt = await Db.ScalarAsync<DateTime>(
            "SELECT expired_at FROM holds WHERE id = @id", ("id", Guid.ParseExact(expired[2..], "N")));
        Assert.True(expiredAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task A_hold_confirmed_before_it_expires_is_never_expired_by_the_sweeper()
    {
        var classId = await Api.CreateClassAsync("Paid in time", 2);
        var holdId = await Api.HoldIdAsync(classId, Student(20));
        Assert.Equal(HttpStatusCode.OK, (await Api.ConfirmAsync(holdId, Student(20))).StatusCode);

        await Task.Delay(TimeSpan.FromSeconds(3.5));

        Assert.Equal("CONFIRMED", await StoredStatusAsync(holdId));
        var cls = await Api.GetClassAsync(classId);
        Assert.Equal(1, cls.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(1, cls.GetProperty("availableSeats").GetInt32());
    }
}
