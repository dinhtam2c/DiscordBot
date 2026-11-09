using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Modules.Music.Processes;

namespace Modules.Music.Playback.Native.Ffmpeg;

internal sealed class FfmpegStream(
    Process process,
    Task? inputPump,
    CancellationTokenSource? inputPumpLifetime,
    ILogger logger)
    : ProcessOutputStreamBase(process, "ffmpeg", inputPump, logger, inputPumpLifetime);
