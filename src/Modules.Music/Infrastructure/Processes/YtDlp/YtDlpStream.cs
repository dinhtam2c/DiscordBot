using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Modules.Music.Infrastructure.Processes.YtDlp;

internal sealed class YtDlpStream(Process process, ILogger logger)
    : ProcessOutputStreamBase(process, "yt-dlp", inputPump: null, logger);
