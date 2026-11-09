using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace SchoolAdmission.Api.Tests.Infrastructure;

public static class HttpResponseExtensions
{
    /// <summary>Reads the body as raw JSON so tests can assert exact property names.</summary>
    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }
}

/// <summary>Small typed helpers over the HTTP API used by the integration tests.</summary>
public sealed class ApiClient(HttpClient http)
{
    public HttpClient Http { get; } = http;

    public static string StudentId(int n) => $"2015{n:D13}";

    public Task<HttpResponseMessage> PostRawAsync(string path, string body) =>
        Http.PostAsync(path, new StringContent(body, Encoding.UTF8, "application/json"));

    public async Task<string> CreateClassAsync(string name = "Class 6 - Morning Shift", int seats = 10)
    {
        var response = await Http.PostAsJsonAsync("/classes", new { name, totalSeats = seats });
        response.EnsureSuccessStatusCode();
        return (await response.ReadJsonAsync()).GetProperty("id").GetString()!;
    }

    public async Task<JsonElement> GetClassAsync(string classId)
    {
        var response = await Http.GetAsync($"/classes/{classId}");
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync();
    }
}
