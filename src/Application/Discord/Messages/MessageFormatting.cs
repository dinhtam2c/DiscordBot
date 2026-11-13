namespace Application.Discord.Messages;

public static class MessageFormatting
{
    // Use for raw values inside Markdown descriptions, fields, and message content.
    // Embed titles and footers are plain text and should receive the raw value.
    public static string Text(string value)
        => value.Replace("\\", "\\\\")
            .Replace("*", "\\*")
            .Replace("_", "\\_")
            .Replace("~", "\\~")
            .Replace("`", "\\`")
            .Replace("||", "|\u200B|")
            .Replace("](", "]\u200B(")
            .Replace("<", "<\u200B");

    // Accept a raw label, never the result of Text, to avoid escaping twice.
    public static string Link(string label, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return Text(label);

        var target = uri.AbsoluteUri.Replace("(", "%28").Replace(")", "%29");
        return $"[{Text(label)}]({target})";
    }
}
