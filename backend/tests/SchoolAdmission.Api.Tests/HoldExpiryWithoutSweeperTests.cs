using System.Net;
using SchoolAdmission.Api.Tests.Infrastructure;

namespace SchoolAdmission.Api.Tests;

/// <summary>
/// The sweep interval is an hour, so the background job can never help: expiry must be correct
/// purely because every query judges holds against the database clock.
/// </summary>
public class HoldExpiryWithoutSweeperTests(PostgresDatabaseFixture database) : ApiTestBase(database)
{
    protected override int HoldSeconds => 2;
    protected override int SweepSeconds => 3600;

    private static string Student(int n) => ApiClient.StudentId(n);

    [Fact]
    public async Task Expired_holds_release_their_seat_and_cannot_be_used_though_the_stored_row_never_changes()
    {
        var classId = await Api.CreateClassAsync("No sweeper", 1);
        var holdId = await Api.HoldIdAsync(classId, Student(3));

        await Task.Delay(TimeSpan.FromSeconds(3.5));

        var stored = await Db.ScalarAsync<string>(
            "SELECT status FROM holds WHERE id = @id", ("id", Guid.ParseExact(holdId[2..], "N")));
        Assert.Equal("HELD", stored); // nothing rewrote the row

        var hold = await (await Api.GetAsync($"/holds/{holdId}")).ReadJsonAsync();
        Assert.Equal("EXPIRED", hold.GetProperty("status").GetString());
        var mine = await (await Api.GetAsync("/me/holds", Student(3))).ReadJsonAsync();
        Assert.Equal("EXPIRED", mine[0].GetProperty("status").GetString());
        Assert.Equal(1, (await Api.GetClassAsync(classId)).GetProperty("availableSeats").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await Api.ConfirmAsync(holdId, Student(3))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Api.CancelAsync(holdId, Student(3))).StatusCode);

        // The seat really is free: another student gets it, and so can the first one for another class.
        Assert.Equal(HttpStatusCode.Created, (await Api.HoldAsync(classId, Student(4))).StatusCode);
    }

    [Fact]
    public async Task A_student_whose_unswept_hold_expired_can_apply_again()
    {
        var classId = await Api.CreateClassAsync("Reapply without sweeper", 2);
        await Api.HoldIdAsync(classId, Student(5));

        await Task.Delay(TimeSpan.FromSeconds(3.5));
        var again = await Api.HoldAsync(classId, Student(5));

        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }
}
