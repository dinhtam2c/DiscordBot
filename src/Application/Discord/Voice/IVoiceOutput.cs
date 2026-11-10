namespace Application.Discord.Voice;

public interface IVoiceOutput
{
    Task WritePcmAsync(ReadOnlyMemory<byte> data, CancellationToken ct);

    Task FlushAsync(CancellationToken ct);

    Task ClearAsync(CancellationToken ct);
}
