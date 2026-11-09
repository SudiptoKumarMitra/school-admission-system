using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Data.Entities;
using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Services;

public class ClassService(AdmissionDbContext db)
{
    // Column aliases match the property names so SqlQuery can materialize rows directly.
    private sealed record ClassRow(Guid Id, string Name, int TotalSeats, DateTimeOffset CreatedAt, int Held, int Confirmed);

    public async Task<ClassResponse> CreateAsync(CreateClassRequest request, CancellationToken ct)
    {
        var entity = new ClassEntity { Id = Ids.NewId(), Name = request.Name, TotalSeats = request.TotalSeats };
        db.Classes.Add(entity);
        await db.SaveChangesAsync(ct);

        return ToResponse(new ClassRow(entity.Id, entity.Name, entity.TotalSeats, entity.CreatedAt, 0, 0));
    }

    public async Task<IReadOnlyList<ClassResponse>> ListAsync(CancellationToken ct)
    {
        // Held seats count only unexpired HELD holds: expiry is judged by the database clock, so
        // seats come back the moment a hold times out, with no background job involved.
        var rows = await db.Database.SqlQuery<ClassRow>($"""
            SELECT c.id AS "Id", c.name AS "Name", c.total_seats AS "TotalSeats", c.created_at AS "CreatedAt",
                   (count(h.id) FILTER (WHERE h.status = 'HELD' AND h.expires_at > now()))::int AS "Held",
                   (count(h.id) FILTER (WHERE h.status = 'CONFIRMED'))::int AS "Confirmed"
            FROM classes c
            LEFT JOIN holds h ON h.class_id = c.id
            GROUP BY c.id
            ORDER BY c.created_at, c.id
            """).ToListAsync(ct);

        return rows.Select(ToResponse).ToList();
    }

    public async Task<ClassResponse?> GetAsync(Guid id, CancellationToken ct)
    {
        var rows = await db.Database.SqlQuery<ClassRow>($"""
            SELECT c.id AS "Id", c.name AS "Name", c.total_seats AS "TotalSeats", c.created_at AS "CreatedAt",
                   (count(h.id) FILTER (WHERE h.status = 'HELD' AND h.expires_at > now()))::int AS "Held",
                   (count(h.id) FILTER (WHERE h.status = 'CONFIRMED'))::int AS "Confirmed"
            FROM classes c
            LEFT JOIN holds h ON h.class_id = c.id
            WHERE c.id = {id}
            GROUP BY c.id
            """).ToListAsync(ct);

        return rows.Count == 0 ? null : ToResponse(rows[0]);
    }

    private static ClassResponse ToResponse(ClassRow row) => new(
        Ids.FormatClass(row.Id),
        row.Name,
        row.TotalSeats,
        row.Held,
        row.Confirmed,
        Math.Max(0, row.TotalSeats - row.Held - row.Confirmed),
        row.CreatedAt);
}
