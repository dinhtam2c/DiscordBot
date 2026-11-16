namespace Application.Discord.Messages;

public static class MessageFormatting
{
    // Use for text values inside Markdown descriptions, fields, and message content.
    // Preserve existing escapes so formatting a value again does not add backslashes.
    // Embed titles and footers are plain text and should receive the raw value.
    public static string Text(string? value)
    {
        var escaped = new System.Text.StringBuilder();
        value ??= string.Empty;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character == '\\' && index + 1 < value.Length && "\\*_~`".Contains(value[index + 1]))
            {
                escaped.Append(character).Append(value[++index]);
                continue;
            }
            if ("\\*_~`".Contains(character))
                escaped.Append('\\');
            if (character == '|' && index > 0 && value[index - 1] == '|')
                escaped.Append('\u200B');
            escaped.Append(character);
            if (character == '<' && (index + 1 == value.Length || value[index + 1] != '\u200B'))
                escaped.Append('\u200B');
        }

        return escaped.ToString()
            .Replace("](", "]\u200B(");
    }

    // Accept a raw label, never the result of Text, to avoid escaping twice.
    public static string Link(string? label, string? url)
    {
        label = string.IsNullOrWhiteSpace(label) ? url ?? "Unknown" : label;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return Text(label);

        // Break spoiler delimiters without adding visible escapes to the link label.
        label = System.Text.RegularExpressions.Regex.Replace(label, @"(?<=\|)\|", "\u200B|");

        // Discord displays backslash escapes literally in masked-link labels.
        // Keep simple labels raw. Ambiguous labels retain a separate, safe link.
        var target = uri.AbsoluteUri.Replace("(", "%28").Replace(")", "%29");
        if (!CanUseLinkLabel(label))
            return $"{Text(label)} · [Open]({target})";

        return $"[{label}]({target})";
    }

    private static bool CanUseLinkLabel(string label)
    {
        if (label.IndexOfAny(['\\', '`', '<', '>', '\r', '\n']) >= 0
            || label.Contains("||") || label.Contains("]("))
            return false;

        foreach (var marker in "*_~")
            if (label.Count(character => character == marker) > 1)
                return false;

        var brackets = 0;
        foreach (var character in label)
        {
            if (character == '[')
                brackets++;
            else if (character == ']' && --brackets < 0)
                return false;
        }
        return brackets == 0;
    }
}
