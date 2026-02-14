using System.Data.Common;
using System.Net;
using System.Text.Json;
using HotsPatchNotes.Shared;
using HotsPatchNotes.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace HotsPatchNotes.Api.Middleware;

/// <summary>
/// Global exception handling middleware that catches unhandled exceptions and returns structured error responses.
/// </summary>
public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IWebHostEnvironment env)
{
    /// <summary>
    /// Invokes the middleware to handle exceptions globally.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    /// <summary>
    /// Handles exceptions and returns appropriate error responses.
    /// </summary>
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            DbUpdateException or DbException => (HttpStatusCode.ServiceUnavailable, Constants.ErrorMessages.DatabaseUnavailable),
            OperationCanceledException => ((HttpStatusCode)499, Constants.ErrorMessages.RequestCancelled),
            ArgumentException or InvalidOperationException => (HttpStatusCode.BadRequest, exception.Message),
            _ => (HttpStatusCode.InternalServerError, Constants.ErrorMessages.InternalServerError)
        };

        // Log the exception with structured logging
        logger.LogError(
            exception,
            "Unhandled exception occurred. TraceId: {TraceId}, StatusCode: {StatusCode}, Message: {Message}",
            context.TraceIdentifier,
            (int)statusCode,
            message);

        // Set response properties
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        // Create error response
        var errorResponse = new ErrorResponseDto
        {
            Message = message,
            // Only include exception details in development mode for security
            Detail = env.IsDevelopment() ? exception.ToString() : null
        };

        // Serialize and write response
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, options));
    }
}
