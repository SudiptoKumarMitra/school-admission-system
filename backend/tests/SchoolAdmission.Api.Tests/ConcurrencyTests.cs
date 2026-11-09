using System.Net;
using SchoolAdmission.Api.Tests.Infrastructure;

namespace SchoolAdmission.Api.Tests;

/// <summary>
/// Many simultaneous requests against a real PostgreSQL: seats must never be oversold and a
/// student must never end up with two active applications in one class.
/// </summary>
public class ConcurrencyTests(PostgresDatabaseFixture database) : ApiTestBase(database)
{
    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.ReadJsonAsync()).GetProperty("code").GetString();

    [Fact]
    public async Task Two_hundred_students_racing_for_ten_seats_get_exactly_ten()
    {
        const int seats = 10;
        const int students = 200;
        var classId = await Api.CreateClassAsync("Class 6 - Morning Shift", seats);

        var responses = await Concurrent.RunAsync(students, i => Api.HoldAsync(classId, ApiClient.StudentId(i)));

        var created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflicts = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToList();
        Assert.Equal(seats, created);
        Assert.Equal(students - seats, conflicts.Count);
        Assert.Equal(students, created + conflicts.Count); // nothing else: no 500s, no timeouts
        foreach (var conflict in conflicts)
        {
            Assert.Equal("NO_SEATS_AVAILABLE", await CodeOfAsync(conflict));
        }

        var json = await Api.GetClassAsync(classId);
        Assert.Equal(seats, json.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, json.GetProperty("availableSeats").GetInt32());
        Assert.Equal((long)seats, await Db.CountHoldsAsync(classId, "HELD", "CONFIRMED"));
    }

    [Fact]
    public async Task Several_classes_filling_at_once_each_stop_at_their_own_capacity()
    {
        const int classes = 5;
        const int seats = 3;
        const int studentsPerClass = 40;
        var classIds = new List<string>();
        for (var c = 0; c < classes; c++)
        {
            classIds.Add(await Api.CreateClassAsync($"Class {c}", seats));
        }

        var responses = await Concurrent.RunAsync(classes * studentsPerClass, i =>
            Api.HoldAsync(classIds[i % classes], ApiClient.StudentId(i)));

        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict));
        foreach (var classId in classIds)
        {
            Assert.Equal((long)seats, await Db.CountHoldsAsync(classId, "HELD", "CONFIRMED"));
        }
    }

    [Fact]
    public async Task One_student_applying_many_times_at_once_gets_a_single_application()
    {
        var classId = await Api.CreateClassAsync("Single student", 50);
        var student = ApiClient.StudentId(7);

        var responses = await Concurrent.RunAsync(25, _ => Api.HoldAsync(classId, student));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        var rejected = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToList();
        Assert.Equal(24, rejected.Count);
        foreach (var response in rejected)
        {
            Assert.Equal("ACTIVE_HOLD_EXISTS", await CodeOfAsync(response));
        }

        Assert.Equal(1L, await Db.CountHoldsAsync(classId, "HELD"));
    }

    [Fact]
    public async Task Racing_students_with_repeat_attempts_never_exceed_capacity()
    {
        const int seats = 5;
        var classId = await Api.CreateClassAsync("Mixed", seats);

        // 60 students, each trying twice at the same moment.
        var responses = await Concurrent.RunAsync(120, i => Api.HoldAsync(classId, ApiClient.StudentId(i % 60)));

        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict));
        Assert.Equal(seats, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal((long)seats, await Db.CountHoldsAsync(classId, "HELD", "CONFIRMED"));
        var distinctStudents = await Db.ScalarAsync<long>(
            "SELECT count(DISTINCT student_id) FROM holds WHERE class_id = @c AND status = 'HELD'",
            ("c", Guid.ParseExact(classId[2..], "N")));
        Assert.Equal((long)seats, distinctStudents);
    }
}
