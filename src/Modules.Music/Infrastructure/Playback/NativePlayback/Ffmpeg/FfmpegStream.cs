using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Modules.Music.Infrastructure.Processes;

namespace Modules.Music.Infrastructure.Playback.NativePlayback.Ffmpeg;

internal sealed class FfmpegStream(
    Process process,
    Task? inputPump,
    CancellationTokenSource? inputPumpLifetime,
    ILogger logger)
    : ProcessOutputStreamBase(process, "ffmpeg", inputPump, logger, inputPumpLifetime);
