using GigaChat.Net;
using GigaChat.Net.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGigaChat(builder.Configuration);

// Optional: context values can come from headers, query, claims, route values, or your own services.
// With AllowModelOverrideFromHeader enabled, X-GigaChat-Model is copied automatically.
builder.Services.Configure<GigaChatRequestContextOptions>(options =>
{
    options.ConfigureContext = (httpContext, context) =>
    {
        if (httpContext.Request.Query.TryGetValue("requestId", out var requestId))
            context.RequestId = requestId.ToString();

        if (httpContext.Request.Query.TryGetValue("sessionId", out var sessionId))
            context.SessionId = sessionId.ToString();

        var userClientId = httpContext.User.FindFirst("client_id")?.Value;
        if (!string.IsNullOrWhiteSpace(userClientId))
            context.ClientId = userClientId;
    };
});

// Optional: use this block only when your ASP.NET Core app already has a custom HttpClient pipeline.
// builder.Services.AddHttpClient("GigaChat", client =>
// {
//     client.Timeout = TimeSpan.FromSeconds(60);
// });
//
// builder.Services.AddGigaChat(
//     builder.Configuration,
//     provider => provider.GetRequiredService<IHttpClientFactory>().CreateClient("GigaChat"));

var app = builder.Build();

app.UseGigaChatContext();

app.MapGet("/", () => Results.Ok(new
{
    Name = "GigaChat ASP.NET Core example",
    Endpoints = new[] { "POST /chat", "GET /models" }
}));

app.MapPost("/chat", async (
    ChatRequest request,
    IGigaChatClient client,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { Error = "Message is required." });

    var headers = request.RequestId is null && request.SessionId is null
        ? null
        : new GigaChatRequestHeaders
        {
            RequestId = request.RequestId,
            SessionId = request.SessionId
        };

    var response = await client.ChatAsync(request.Message, headers, cancellationToken);
    var message = response.Choices.FirstOrDefault()?.Message.Content;

    return Results.Ok(new ChatResponse(message ?? string.Empty));
});

app.MapGet("/models", async (IGigaChatClient client, CancellationToken cancellationToken) =>
{
    var models = await client.GetModelsAsync(cancellationToken);
    return Results.Ok(models.Data.Select(model => new ModelResponse(model.Id, model.OwnedBy)));
});

app.Run();

public sealed record ChatRequest(string Message, string? RequestId = null, string? SessionId = null);

public sealed record ChatResponse(string Message);

public sealed record ModelResponse(string Id, string OwnedBy);
