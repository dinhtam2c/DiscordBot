using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Modules.Music.Infrastructure.Processes;

namespace Modules.Music.Infrastructure.AudioSource.Youtube;

internal sealed class YtDlpStream(Process process, ILogger logger)
    : ProcessOutputStreamBase(process, "yt-dlp", inputPump: null, logger);
