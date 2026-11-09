namespace SchoolAdmission.Api.Data.Entities;

/// <summary>One class (each shift is its own class) with a fixed number of seats.</summary>
public class ClassEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalSeats { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
