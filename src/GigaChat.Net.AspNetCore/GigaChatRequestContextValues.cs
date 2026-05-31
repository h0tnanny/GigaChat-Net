namespace GigaChat.Net.AspNetCore;

/// <summary>
/// Mutable request context values that will be applied to <see cref="GigaChatContext"/>
/// for the current ASP.NET Core async flow.
/// </summary>
public sealed class GigaChatRequestContextValues
{
    /// <inheritdoc cref="GigaChatContext.Authorization" />
    public string? Authorization { get; set; }

    /// <inheritdoc cref="GigaChatContext.ClientId" />
    public string? ClientId { get; set; }

    /// <inheritdoc cref="GigaChatContext.RequestId" />
    public string? RequestId { get; set; }

    /// <inheritdoc cref="GigaChatContext.SessionId" />
    public string? SessionId { get; set; }

    /// <inheritdoc cref="GigaChatContext.ServiceId" />
    public string? ServiceId { get; set; }

    /// <inheritdoc cref="GigaChatContext.OperationId" />
    public string? OperationId { get; set; }

    /// <inheritdoc cref="GigaChatContext.TraceId" />
    public string? TraceId { get; set; }

    /// <inheritdoc cref="GigaChatContext.AgentId" />
    public string? AgentId { get; set; }

    /// <inheritdoc cref="GigaChatContext.Model" />
    public string? Model { get; set; }

    /// <inheritdoc cref="GigaChatContext.CustomHeaders" />
    public IReadOnlyDictionary<string, string>? CustomHeaders { get; set; }

    /// <inheritdoc cref="GigaChatContext.ChatUrl" />
    public string? ChatUrl { get; set; }

    internal static GigaChatRequestContextValues Capture()
    {
        return new GigaChatRequestContextValues
        {
            Authorization = GigaChatContext.Authorization,
            ClientId = GigaChatContext.ClientId,
            RequestId = GigaChatContext.RequestId,
            SessionId = GigaChatContext.SessionId,
            ServiceId = GigaChatContext.ServiceId,
            OperationId = GigaChatContext.OperationId,
            TraceId = GigaChatContext.TraceId,
            AgentId = GigaChatContext.AgentId,
            Model = GigaChatContext.Model,
            CustomHeaders = GigaChatContext.CustomHeaders,
            ChatUrl = GigaChatContext.ChatUrl
        };
    }

    internal void Apply()
    {
        GigaChatContext.Authorization = Authorization;
        GigaChatContext.ClientId = ClientId;
        GigaChatContext.RequestId = RequestId;
        GigaChatContext.SessionId = SessionId;
        GigaChatContext.ServiceId = ServiceId;
        GigaChatContext.OperationId = OperationId;
        GigaChatContext.TraceId = TraceId;
        GigaChatContext.AgentId = AgentId;
        GigaChatContext.Model = Model;
        GigaChatContext.CustomHeaders = CustomHeaders;

        if (ChatUrl is not null)
            GigaChatContext.ChatUrl = ChatUrl;
    }
}
