using System.Net.Http.Json;

namespace ApisField.Api.Tests;

/// <summary>Testes de GET /api/health.</summary>
public class HealthTests
{
    [Fact]
    public async Task Health_DevolveOk()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/health");

        res.EnsureSuccessStatusCode();
        Assert.Equal("ok", (await res.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["status"]);
    }
}
