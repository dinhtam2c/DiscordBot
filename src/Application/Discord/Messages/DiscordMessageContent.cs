using Discord;

namespace Application.Discord.Messages;

public static class DiscordMessageContent
{
    public static MessageComponent Create(string? text = null, MessageComponent? components = null)
    {
        if (components is not null)
        {
            if (!string.IsNullOrEmpty(text))
                throw new ArgumentException("Put message text inside the components when providing a layout.", nameof(text));
            return components;
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return new ComponentBuilderV2().WithTextDisplay(text).Build();
    }

    public static void Apply(MessageProperties message, string? text = null, MessageComponent? components = null)
    {
        message.Components = Create(text, components);
        // Clear any content left by a deferred reply or an older message before enabling V2.
        message.Content = null;
        message.Embeds = Array.Empty<Embed>();
        message.AllowedMentions = AllowedMentions.None;
        message.Flags = MessageFlags.ComponentsV2;
    }
}
