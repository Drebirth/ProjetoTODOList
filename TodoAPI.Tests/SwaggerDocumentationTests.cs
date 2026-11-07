using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using Xunit;

namespace TodoAPI.Tests;

public sealed class SwaggerDocumentationTests
{
    [Fact]
    public async Task Development_serves_swagger_ui_and_documents_jwt_only_for_protected_routes()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"todo-api-swagger-{Guid.NewGuid():N}.db");

        try
        {
            using var factory = CreateFactory(Environments.Development, databasePath);
            using var client = factory.CreateClient();

            using var uiResponse = await client.GetAsync("/swagger");
            Assert.Equal(System.Net.HttpStatusCode.OK, uiResponse.StatusCode);
            Assert.Contains("Swagger UI", await uiResponse.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

            using var documentResponse = await client.GetAsync("/openapi/v1.json");
            Assert.Equal(System.Net.HttpStatusCode.OK, documentResponse.StatusCode);

            using var document = JsonDocument.Parse(await documentResponse.Content.ReadAsStringAsync());
            var root = document.RootElement;
            Assert.Equal("TodoAPI", root.GetProperty("info").GetProperty("title").GetString());
            Assert.True(root.GetProperty("paths").TryGetProperty("/api/auth/login", out _));

            var schemes = root.GetProperty("components").GetProperty("securitySchemes");
            Assert.Equal("http", schemes.GetProperty("Bearer").GetProperty("type").GetString());
            Assert.Equal("bearer", schemes.GetProperty("Bearer").GetProperty("scheme").GetString());

            var todosGet = root.GetProperty("paths").GetProperty("/api/todos").GetProperty("get");
            Assert.True(todosGet.TryGetProperty("security", out var security));
            Assert.Contains(security.EnumerateArray(), requirement =>
                requirement.TryGetProperty("Bearer", out _));

            var loginPost = root.GetProperty("paths").GetProperty("/api/auth/login").GetProperty("post");
            Assert.False(loginPost.TryGetProperty("security", out _));
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Swagger_and_openapi_document_are_not_served_outside_development()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"todo-api-no-swagger-{Guid.NewGuid():N}.db");

        try
        {
            using var factory = CreateFactory(Environments.Production, databasePath);
            using var client = factory.CreateClient();

            using var uiResponse = await client.GetAsync("/swagger");
            using var documentResponse = await client.GetAsync("/openapi/v1.json");

            Assert.Equal(System.Net.HttpStatusCode.NotFound, uiResponse.StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.NotFound, documentResponse.StatusCode);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(string environment, string databasePath) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:TodoDatabase"] = $"Data Source={databasePath}",
                    ["Jwt:Issuer"] = "TodoApi.SwaggerTests",
                    ["Jwt:Audience"] = "TodoApi.SwaggerTests.Client",
                    ["Jwt:SigningKey"] = "swagger-test-signing-key-that-is-at-least-32-bytes"
                });
            });
        });

    private static void DeleteDatabase(string databasePath)
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }
}
