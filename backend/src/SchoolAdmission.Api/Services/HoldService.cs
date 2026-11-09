using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolAdmission.Api.Configuration;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Data.Entities;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Http;

namespace SchoolAdmission.Api.Services;

public class HoldService(AdmissionDbContext db, AppOptions options, ILogger<HoldService> logger)
{
    private sealed record LockedClass(string Name, int TotalSeats);
    private sealed record TextRow(string Value);
    private sealed record IntRow(int Value);
    private sealed record TimeRow(DateTimeOffset Value);

    private sealed record HoldRow(
        Guid Id, Guid ClassId, string ClassName, string Status,
        DateTimeOffset ExpiresAt, DateTimeOffset CreatedAt, DateTimeOffset? ConfirmedAt, DateTimeOffset ServerTime);

    /// <summary>
    /// Places a seat hold. Every writer that can add to held + confirmed for a class first locks that
    /// class's row, so the "is there a seat?" check and the insert are atomic per class, across any
    /// number of API processes. Releasing a seat (expiry, cancel) never needs the lock.
    /// </summary>
    public async Task<ServiceResult<CreateHoldResponse>> CreateHoldAsync(
        Guid classId, string studentId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var lockedClass = (await db.Database.SqlQuery<LockedClass>($"""
            SELECT name AS "Name", total_seats AS "TotalSeats" FROM classes WHERE id = {classId} FOR UPDATE
            """).ToListAsync(ct)).SingleOrDefault();
        if (lockedClass is null)
        {
            return ServiceResult<CreateHoldResponse>.Fail(ApiErrors.ClassNotFound());
        }

        // clock_timestamp() (not now()) so waiting for the lock cannot make expiry decisions stale.
        var now = (await db.Database.SqlQuery<TimeRow>($"""SELECT clock_timestamp() AS "Value" """)
            .ToListAsync(ct)).Single().Value;

        // Tidy this class: holds past their expiry stop occupying seats.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE holds SET status = 'EXPIRED', expired_at = {now}
            WHERE class_id = {classId} AND status = 'HELD' AND expires_at <= {now}
            """, ct);

        var existing = (await db.Database.SqlQuery<TextRow>($"""
            SELECT status AS "Value" FROM holds
            WHERE class_id = {classId} AND student_id = {studentId} AND status IN ('HELD', 'CONFIRMED')
            """).ToListAsync(ct)).SingleOrDefault();
        if (existing is not null)
        {
            return ServiceResult<CreateHoldResponse>.Fail(
                existing.Value == HoldStatus.Confirmed ? ApiErrors.AlreadyConfirmed() : ApiErrors.ActiveHoldExists());
        }

        var taken = (await db.Database.SqlQuery<IntRow>($"""
            SELECT count(*)::int AS "Value" FROM holds
            WHERE class_id = {classId} AND status IN ('HELD', 'CONFIRMED')
            """).ToListAsync(ct)).Single().Value;
        if (taken >= lockedClass.TotalSeats)
        {
            return ServiceResult<CreateHoldResponse>.Fail(ApiErrors.NoSeatsAvailable(lockedClass.Name));
        }

        var hold = new HoldEntity
        {
            Id = Ids.NewId(),
            ClassId = classId,
            StudentId = studentId,
            Status = HoldStatus.Held,
            CreatedAt = now,
            ExpiresAt = now.AddSeconds(options.HoldDurationSeconds),
        };
        db.Holds.Add(hold);

        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Second line of defence: the partial unique index caught a duplicate active application.
            return ServiceResult<CreateHoldResponse>.Fail(ApiErrors.ActiveHoldExists());
        }

        logger.LogInformation("Hold {HoldId} created for student {Student} in class {ClassId}",
            Ids.FormatHold(hold.Id), StudentId.Mask(studentId), Ids.FormatClass(classId));

        return ServiceResult<CreateHoldResponse>.Ok(new CreateHoldResponse(
            Ids.FormatHold(hold.Id),
            Ids.FormatClass(classId),
            studentId,
            hold.Status,
            hold.ExpiresAt,
            lockedClass.Name,
            hold.CreatedAt,
            now));
    }

    /// <summary>Returns the hold with its effective status (HELD past its expiry reads as EXPIRED).</summary>
    public async Task<HoldResponse?> GetAsync(Guid holdId, CancellationToken ct)
    {
        var rows = await db.Database.SqlQuery<HoldRow>($"""
            SELECT h.id AS "Id", h.class_id AS "ClassId", c.name AS "ClassName",
                   CASE WHEN h.status = 'HELD' AND h.expires_at <= now() THEN 'EXPIRED' ELSE h.status END AS "Status",
                   h.expires_at AS "ExpiresAt", h.created_at AS "CreatedAt", h.confirmed_at AS "ConfirmedAt",
                   now() AS "ServerTime"
            FROM holds h JOIN classes c ON c.id = h.class_id
            WHERE h.id = {holdId}
            """).ToListAsync(ct);

        return rows.Count == 0 ? null : ToHoldResponse(rows[0]);
    }

    /// <summary>All of one student's applications, newest first, with effective statuses.</summary>
    public async Task<IReadOnlyList<MyHoldItem>> ListForStudentAsync(string studentId, CancellationToken ct)
    {
        var rows = await db.Database.SqlQuery<HoldRow>($"""
            SELECT h.id AS "Id", h.class_id AS "ClassId", c.name AS "ClassName",
                   CASE WHEN h.status = 'HELD' AND h.expires_at <= now() THEN 'EXPIRED' ELSE h.status END AS "Status",
                   h.expires_at AS "ExpiresAt", h.created_at AS "CreatedAt", h.confirmed_at AS "ConfirmedAt",
                   now() AS "ServerTime"
            FROM holds h JOIN classes c ON c.id = h.class_id
            WHERE h.student_id = {studentId}
            ORDER BY h.created_at DESC, h.id DESC
            """).ToListAsync(ct);

        return rows.Select(r => new MyHoldItem(
            Ids.FormatHold(r.Id), Ids.FormatClass(r.ClassId), r.ClassName, r.Status,
            r.ExpiresAt, r.CreatedAt, r.ConfirmedAt, r.ServerTime)).ToList();
    }

    private static HoldResponse ToHoldResponse(HoldRow r) => new(
        Ids.FormatHold(r.Id), Ids.FormatClass(r.ClassId), r.Status,
        r.ExpiresAt, r.ClassName, r.CreatedAt, r.ConfirmedAt, r.ServerTime);
}
