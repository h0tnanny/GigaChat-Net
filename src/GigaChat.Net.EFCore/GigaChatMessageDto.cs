using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GigaChat.Net.EFCore;

public sealed class GigaChatMessageDto
{
    public string Role { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;

    public static GigaChatMessageDto FromMessage(ChatMessageContent msg)
    {
        ArgumentNullException.ThrowIfNull(msg);
        return new() { Role = msg.Role.Label, Content = msg.Content ?? string.Empty };
    }

    public ChatMessageContent ToMessage() =>
        new(new AuthorRole(Role), Content);
}
