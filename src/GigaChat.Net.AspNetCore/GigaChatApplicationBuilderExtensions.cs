using Microsoft.AspNetCore.Builder;

namespace GigaChat.Net.AspNetCore;

/// <summary>
/// ASP.NET Core pipeline helpers for GigaChat.Net.
/// </summary>
public static class GigaChatApplicationBuilderExtensions
{
    /// <summary>
    /// Adds middleware that copies selected request metadata into <see cref="GigaChatContext"/>.
    /// </summary>
    public static IApplicationBuilder UseGigaChatContext(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<GigaChatRequestContextMiddleware>();
    }
}
