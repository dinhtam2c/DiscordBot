namespace Modules.Music.Tracks.AudioSources;

internal sealed record ResolvedAudioSource(string Url, IReadOnlyDictionary<string, string> Headers, bool IsLive = false);
