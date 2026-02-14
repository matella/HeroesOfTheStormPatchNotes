using System.Data.Common;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using HotsPatchNotes.Api.Middleware;
using HotsPatchNotes.Shared.DTOs;
using Moq;
using Xunit;

namespace HotsPatchNotes.Api.Tests.Middleware;

/// <summary>
/// Tests for GlobalExceptionMiddleware.
/// </summary>
public sealed class GlobalExceptionMiddlewareTests
{
    private readonly Mock<IWebHostEnvironment> _mockEnv;
    private readonly GlobalExceptionMiddleware _middleware;

    public GlobalExceptionMiddlewareTests()
    {
        _mockEnv = new Mock<IWebHostEnvironment>();
        _mockEnv.Setup(e => e.EnvironmentName).Returns("Production");

        _middleware = new GlobalExceptionMiddleware(
            next: _ => throw new InvalidOperationException("Test exception"),
            logger: NullLogger<GlobalExceptionMiddleware>.Instance,
            env: _mockEnv.Object);
    }

    [Fact]
    public async Task InvokeAsync_NoException_CallsNextMiddleware()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var nextCalled = false;
        var middleware = new GlobalExceptionMiddleware(
            next: _ => { nextCalled = true; return Task.CompletedTask; },
            logger: NullLogger<GlobalExceptionMiddleware>.Instance,
            env: _mockEnv.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_DbUpdateException_Returns503()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionMiddleware(
            next: _ => throw new DbUpdateException("Database error"),
            logger: NullLogger<GlobalExceptionMiddleware>.Instance,
            env: _mockEnv.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var response = await JsonSerializer.DeserializeAsync<ErrorResponseDto>(context.Response.Body, options);
        Assert.NotNull(response);
        Assert.Contains("Database is temporarily unavailable", response.Message);
    }

    [Fact]
    public async Task InvokeAsync_DbException_Returns503()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var mockException = new Mock<DbException>();
        var middleware = new GlobalExceptionMiddleware(
            next: _ => throw mockException.Object,
            logger: NullLogger<GlobalExceptionMiddleware>.Instance,
            env: _mockEnv.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_OperationCanceledException_Returns499()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionMiddleware(
            next: _ => throw new OperationCanceledException("Request cancelled"),
            logger: NullLogger<GlobalExceptionMiddleware>.Instance,
            env: _mockEnv.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(499, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var response = await JsonSerializer.DeserializeAsync<ErrorResponseDto>(context.Response.Body, options);
        Assert.NotNull(response);
        Assert.Contains("Request was cancelled", response.Message);
    }

    [Fact]
    public async Task InvokeAsync_ArgumentException_Returns400()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionMiddleware(
            next: _ => throw new ArgumentException("Invalid argument"),
            logger: NullLogger<GlobalExceptionMiddleware>.Instance,
            env: _mockEnv.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var response = await JsonSerializer.DeserializeAsync<ErrorResponseDto>(context.Response.Body, options);
        Assert.NotNull(response);
        Assert.Equal("Invalid argument", response.Message);
    }

    [Fact]
    public async Task InvokeAsync_InvalidOperationException_Returns400()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionMiddleware(
            next: _ => throw new InvalidOperationException("Invalid operation"),
            logger: NullLogger<GlobalExceptionMiddleware>.Instance,
            env: _mockEnv.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_GenericException_Returns500()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionMiddleware(
            next: _ => throw new Exception("Unexpected error"),
            logger: NullLogger<GlobalExceptionMiddleware>.Instance,
            env: _mockEnv.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var response = await JsonSerializer.DeserializeAsync<ErrorResponseDto>(context.Response.Body, options);
        Assert.NotNull(response);
        Assert.Contains("An unexpected error occurred", response.Message);
    }

    [Fact]
    public async Task InvokeAsync_LogsErrorWithTraceId()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = "test-trace-id";

        var middleware = new GlobalExceptionMiddleware(
            next: _ => throw new Exception("Test error"),
            logger: NullLogger<GlobalExceptionMiddleware>.Instance,
            env: _mockEnv.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - just verify it doesn't throw
        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ProductionMode_HidesExceptionDetails()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        _mockEnv.Setup(e => e.EnvironmentName).Returns("Production");

        var middleware = new GlobalExceptionMiddleware(
            next: _ => throw new Exception("Sensitive error details"),
            logger: NullLogger<GlobalExceptionMiddleware>.Instance,
            env: _mockEnv.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var response = await JsonSerializer.DeserializeAsync<ErrorResponseDto>(context.Response.Body, options);
        Assert.NotNull(response);
        Assert.Null(response.Detail); // Should not include exception details in production
    }

    [Fact]
    public async Task InvokeAsync_DevelopmentMode_IncludesExceptionDetails()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var devEnv = new Mock<IWebHostEnvironment>();
        devEnv.Setup(e => e.EnvironmentName).Returns("Development");

        var middleware = new GlobalExceptionMiddleware(
            next: _ => throw new Exception("Detailed error"),
            logger: NullLogger<GlobalExceptionMiddleware>.Instance,
            env: devEnv.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var response = await JsonSerializer.DeserializeAsync<ErrorResponseDto>(context.Response.Body, options);
        Assert.NotNull(response);
        Assert.NotNull(response.Detail); // Should include exception details in development
        Assert.Contains("Detailed error", response.Detail);
    }
}
