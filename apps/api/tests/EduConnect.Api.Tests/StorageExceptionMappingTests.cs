using System.Net;
using System.Text;
using System.Text.Json;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Common.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EduConnect.Api.Tests;

/// <summary>
/// Phase 7.2 — a thrown StorageException is surfaced as 502 Bad Gateway
/// with a problem-details body. Sanity-checks the mapping in the
/// global exception middleware.
/// </summary>
public class StorageExceptionMappingTests
{
    [Fact]
    public async Task GlobalExceptionMiddleware_maps_StorageException_to_502()
    {
        var (statusCode, body) = await InvokeWithThrowingNext(
            new StorageException("simulated S3 outage"),
            production: false);

        statusCode.Should().Be(502);
        body.Should().Contain("\"status\":502");
        body.Should().Contain("Bad Gateway");
        body.Should().Contain("simulated S3 outage",
            "non-prod responses include the inner detail to aid debugging");
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_redacts_storage_detail_in_production()
    {
        var (statusCode, body) = await InvokeWithThrowingNext(
            new StorageException("simulated S3 outage"),
            production: true);

        statusCode.Should().Be(502);
        body.Should().NotContain("simulated S3 outage");
        body.Should().Contain("Object storage is unavailable.");
    }

    private static async Task<(int Status, string Body)> InvokeWithThrowingNext(
        Exception toThrow, bool production)
    {
        RequestDelegate next = _ => throw toThrow;
        var middleware = new GlobalExceptionMiddleware(
            next,
            NullLogger<GlobalExceptionMiddleware>.Instance,
            new FakeHostEnvironment(production
                ? Environments.Production
                : Environments.Development));

        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        ctx.Request.Path = "/api/attachments/x";

        await middleware.InvokeAsync(ctx);

        ctx.Response.Body.Position = 0;
        using var reader = new StreamReader(ctx.Response.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return (ctx.Response.StatusCode, body);
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string env) { EnvironmentName = env; }
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
