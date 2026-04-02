// =====================================================================================
// GlobalExceptionMiddleware — THE SAFETY NET for the entire API
// =====================================================================================
//
// WHAT IS THIS?
// A middleware that wraps the ENTIRE request pipeline. If anything crashes anywhere
// (controller, orchestrator, tool call, Redis timeout), this catches it and returns
// a clean, structured JSON error instead of ugly stack traces.
//
// WHY IS THIS NEEDED?
// Without it:
//   - Development: Stack traces leak file paths, class names, framework versions (security risk)
//   - Production: Client gets an empty 500 with zero info (terrible debugging experience)
// With it:
//   - Every error returns RFC 7807 ProblemDetails JSON with a correlation ID
//   - The correlation ID links client errors to server logs for fast debugging
//
// HOW EXCEPTION MAPPING WORKS:
//   Azure.RequestFailedException → 502 (Azure service down — not our fault)
//   TaskCanceledException       → 504 (request timed out or client disconnected)
//   JsonException               → 400 (client sent invalid JSON in request body)
//   Everything else             → 500 (something unexpected broke)
//
// INTERVIEW TIP: "Our middleware converts all exceptions to RFC 7807 ProblemDetails with
// correlation IDs. The client never sees stack traces, and we can trace any error in logs
// using the correlation ID."
// =====================================================================================
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace AgenticRAG.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Pass the request to the next middleware/controller in the pipeline.
            // If anything throws, we catch it below.
            await _next(context);
        }
        catch (Exception ex)
        {
            // Generate a short unique ID to correlate this error across client ↔ server logs
            var correlationId = Guid.NewGuid().ToString("N")[..12];
            _logger.LogError(ex, "Unhandled exception [CorrelationId={CorrelationId}]", correlationId);

            // Map exception type to the most appropriate HTTP status code.
            // Pattern matching (C# switch expression) makes this clean and extensible.
            var (statusCode, title) = ex switch
            {
                Azure.RequestFailedException => (StatusCodes.Status502BadGateway, "Upstream Service Error"),
                TaskCanceledException => (StatusCodes.Status504GatewayTimeout, "Request Timeout"),
                JsonException => (StatusCodes.Status400BadRequest, "Invalid Request Format"),
                _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
            };

            // Return RFC 7807 ProblemDetails — the industry standard for HTTP API errors.
            // Includes the correlation ID so the client can reference it in support tickets.
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = "An error occurred while processing your request. Please try again.",
                Instance = context.Request.Path,
                Extensions = { ["correlationId"] = correlationId }
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(problem, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
    }
}
