using Application.Discord.Interactions.Response;
using Discord;
using Discord.WebSocket;

namespace Infrastructure.Discord.DiscordNet;

internal class DiscordInteractionResponder(SocketSlashCommand command) : IInteractionResponder
{
    private bool _isDeferred = false;

    public Task DeferAsync(CancellationToken ct, bool ephemeral = false)
    {
        _isDeferred = true;
        return command.DeferAsync(ephemeral);
    }

    public Task RespondAsync(InteractionResponse response, CancellationToken ct)
    {
        if (command.HasResponded)
        {
            return command.FollowupAsync(
                text: response.Content,
                embeds: [.. response.Embeds.Select(ToEmbed)],
                components: ToComponents(response.Rows),
                ephemeral: response.Ephemeral);
        }

        if (!_isDeferred)
        {
            return command.RespondAsync(
                text: response.Content,
                embeds: [.. response.Embeds.Select(ToEmbed)],
                components: ToComponents(response.Rows),
                ephemeral: response.Ephemeral);
        }
        else
        {
            return command.ModifyOriginalResponseAsync(props =>
            {
                props.Content = response.Content;
                props.Embeds = response.Embeds.Select(ToEmbed).ToArray();
                props.Components = ToComponents(response.Rows);
            });
        }
    }

    private static Embed ToEmbed(ResponseEmbed embed)
    {
        var builder = new EmbedBuilder
        {
            Title = embed.Title,
            Description = embed.Description,
            Url = embed.Url,
            ThumbnailUrl = embed.ThumbnailUrl,
            ImageUrl = embed.ImageUrl
        };

        if (embed.Footer is not null)
        {
            builder.WithFooter(embed.Footer.Text, embed.Footer.IconUrl);
        }

        foreach (var field in embed.Fields)
        {
            builder.AddField(field.Name, field.Value, field.Inline);
        }

        return builder.Build();
    }

    private static MessageComponent? ToComponents(IReadOnlyList<ResponseActionRow> rows)
    {
        if (rows.Count == 0)
        {
            return null;
        }

        if (rows.Count > 5)
        {
            throw new InvalidOperationException("Discord supports at most 5 action rows.");
        }

        var builder = new ComponentBuilder();

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];

            if (row.Components.Count > 5)
            {
                throw new InvalidOperationException("An action row may contain at most 5 components.");
            }

            foreach (var component in row.Components)
            {
                switch (component)
                {
                    case ResponseButton button:
                        builder.WithButton(
                            label: button.Label,
                            customId: button.CustomId,
                            style: ToButtonStyle(button.Style),
                            emote: button.Emoji is null ? null : new Emoji(button.Emoji),
                            disabled: button.Disabled,
                            row: rowIndex);
                        break;

                    case ResponseSelectMenu menu:
                        builder.WithSelectMenu(
                            new SelectMenuBuilder()
                                .WithCustomId(menu.CustomId)
                                .WithPlaceholder(menu.Placeholder)
                                .WithMinValues(menu.MinValues)
                                .WithMaxValues(menu.MaxValues)
                                .WithOptions(
                                    [.. menu.Options.Select(o =>
                                        new SelectMenuOptionBuilder()
                                            .WithLabel(o.Label)
                                            .WithValue(o.Value)
                                            .WithDescription(o.Description)
                                            .WithEmote(o.Emoji is null ? null : new Emoji(o.Emoji))
                                            .WithDefault(o.Default))]),
                            row: rowIndex);
                        break;

                    default:
                        throw new NotSupportedException($"Unsupported response component: {component.GetType().Name}");
                }
            }
        }

        return builder.Build();
    }

    private static global::Discord.ButtonStyle ToButtonStyle(
        Application.Discord.Interactions.Response.ButtonStyle style)
    {
        return style switch
        {
            Application.Discord.Interactions.Response.ButtonStyle.Primary => global::Discord.ButtonStyle.Primary,
            Application.Discord.Interactions.Response.ButtonStyle.Secondary => global::Discord.ButtonStyle.Secondary,
            Application.Discord.Interactions.Response.ButtonStyle.Success => global::Discord.ButtonStyle.Success,
            Application.Discord.Interactions.Response.ButtonStyle.Danger => global::Discord.ButtonStyle.Danger,
            _ => throw new ArgumentOutOfRangeException(nameof(style))
        };
    }
}