using System.Text.Json;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Tests;

public class CreateClassRequestTests
{
    private static bool Parse(string json, out CreateClassRequest? request, out Dictionary<string, string[]> errors)
    {
        using var document = JsonDocument.Parse(json);
        return CreateClassRequest.TryParse(document.RootElement.Clone(), out request, out errors);
    }

    [Fact]
    public void Valid_body_is_parsed_and_the_name_trimmed()
    {
        Assert.True(Parse("""{"name":"  Class 6 ","totalSeats":120}""", out var request, out var errors));

        Assert.Empty(errors);
        Assert.Equal(new CreateClassRequest("Class 6", 120), request);
    }

    [Fact]
    public void Boundary_values_are_accepted()
    {
        Assert.True(Parse("""{"name":"A","totalSeats":1}""", out _, out _));
        Assert.True(Parse($$"""{"name":"{{new string('x', 100)}}","totalSeats":100000}""", out _, out _));
    }

    [Fact]
    public void Every_invalid_field_is_reported_at_once()
    {
        Assert.False(Parse("""{"name":"","totalSeats":0}""", out var request, out var errors));

        Assert.Null(request);
        Assert.Contains("name", errors.Keys);
        Assert.Contains("totalSeats", errors.Keys);
    }
}

public class IdsTests
{
    [Fact]
    public void Class_ids_round_trip()
    {
        var guid = Ids.NewId();

        var text = Ids.FormatClass(guid);

        Assert.StartsWith("c_", text);
        Assert.True(Ids.TryParseClass(text, out var parsed));
        Assert.Equal(guid, parsed);
    }

    [Fact]
    public void Hold_ids_round_trip_and_are_not_interchangeable_with_class_ids()
    {
        var guid = Ids.NewId();

        var text = Ids.FormatHold(guid);

        Assert.True(Ids.TryParseHold(text, out var parsed));
        Assert.Equal(guid, parsed);
        Assert.False(Ids.TryParseClass(text, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("c_")]
    [InlineData("c_123")]
    [InlineData("c_zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    [InlineData("0193a5c2e4b87c3f9a1d2e3f4a5b6c7d")]
    public void Malformed_ids_do_not_parse(string? value) =>
        Assert.False(Ids.TryParseClass(value, out _));
}
