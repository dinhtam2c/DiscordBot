# =========================
# 1. Build .NET application
# =========================

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-noble AS dotnet-build

ARG TARGETARCH

WORKDIR /src

COPY DiscordBot.slnx .
COPY src/Bot/Bot.csproj                         src/Bot/
COPY src/Application/Application.csproj         src/Application/
COPY src/Domain/Domain.csproj                   src/Domain/
COPY src/Infrastructure/Infrastructure.csproj   src/Infrastructure/
COPY src/Modules.Music/Modules.Music.csproj     src/Modules.Music/
COPY src/Modules.Utility/Modules.Utility.csproj src/Modules.Utility/

RUN case "${TARGETARCH}" in \
    amd64) RID="linux-x64" ;; \
    arm64) RID="linux-arm64" ;; \
    *) echo "Unsupported architecture: ${TARGETARCH}" >&2; exit 1 ;; \
    esac && \
    dotnet restore src/Bot/Bot.csproj --runtime "$RID"

COPY . .

RUN case "${TARGETARCH}" in \
    amd64) RID="linux-x64" ;; \
    arm64) RID="linux-arm64" ;; \
    *) echo "Unsupported architecture: ${TARGETARCH}" >&2; exit 1 ;; \
    esac && \
    dotnet publish src/Bot/Bot.csproj \
    -c Release \
    -r "$RID" \
    --self-contained false \
    -o /app/publish \
    --no-restore


# =========================
# 2. Download libdave
# =========================

FROM --platform=$BUILDPLATFORM ubuntu:24.04 AS libdave

ARG TARGETARCH

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    unzip \
    && rm -rf /var/lib/apt/lists/*

RUN case "${TARGETARCH}" in \
    amd64) \
    DAVE_ARCH="X64" \
    DAVE_SHA256="2470a131dbf39a820d893ba3d6f01373fc597868caffd6a30a8746e441ed5ede" \
    ;; \
    arm64) \
    DAVE_ARCH="ARM64" \
    DAVE_SHA256="2848ca62da5c8303626cfa410827f37735455f614256311fc320b35c7c6a1975" \
    ;; \
    *) \
    echo "Unsupported architecture: ${TARGETARCH}" >&2 \
    exit 1 \
    ;; \
    esac && \
    curl -fL \
    "https://github.com/discord/libdave/releases/download/v1.1.1%2Fcpp/libdave-Linux-${DAVE_ARCH}-boringssl.zip" \
    -o /tmp/libdave.zip && \
    echo "${DAVE_SHA256}  /tmp/libdave.zip" | sha256sum -c - && \
    mkdir /out && \
    unzip /tmp/libdave.zip -d /out


# =========================
# 3. Download yt-dlp
# =========================

FROM --platform=$BUILDPLATFORM ubuntu:24.04 AS yt-dlp

ARG TARGETARCH
ARG YTDLP_CHANNEL=stable
ARG YTDLP_STABLE_VERSION=2026.11.23
ARG YTDLP_NIGHTLY_VERSION=2026.11.14.232655

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    && rm -rf /var/lib/apt/lists/*

RUN case "${TARGETARCH}" in \
    amd64)  YTDLP_ASSET="yt-dlp_linux" ;; \
    arm64)  YTDLP_ASSET="yt-dlp_linux_aarch64" ;; \
    *) echo "Unsupported architecture: ${TARGETARCH}" >&2; exit 1 ;; \
    esac && \
    case "${YTDLP_CHANNEL}" in \
    stable) \
    curl -fL \
    "https://github.com/yt-dlp/yt-dlp/releases/download/${YTDLP_STABLE_VERSION}/${YTDLP_ASSET}" \
    -o /out \
    ;; \
    nightly) \
    if [ "${YTDLP_NIGHTLY_VERSION}" = "latest" ]; then \
    YTDLP_URL="https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/latest/download/${YTDLP_ASSET}"; \
    else \
    YTDLP_URL="https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/download/${YTDLP_NIGHTLY_VERSION}/${YTDLP_ASSET}"; \
    fi && \
    curl -fL \
    "${YTDLP_URL}" \
    -o /out \
    ;; \
    *) echo "Unsupported YTDLP_CHANNEL: ${YTDLP_CHANNEL} (use stable or nightly)" >&2; exit 1 ;; \
    esac && \
    chmod 755 /out

# =========================
# 4. Runtime
# =========================

FROM mcr.microsoft.com/dotnet/runtime:10.0.10-noble AS runtime

ARG TARGETARCH

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    ffmpeg \
    libopus0 \
    libsodium23 \
    libssl3 \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Native library compatibility symlinks
RUN OPUS_PATH="$(ldconfig -p | awk '/libopus\.so\.0/{print $NF; exit}')" && \
    SODIUM_PATH="$(ldconfig -p | awk '/libsodium\.so\.23/{print $NF; exit}')" && \
    test -n "$OPUS_PATH" && \
    test -n "$SODIUM_PATH" && \
    ln -sfn "$OPUS_PATH" /usr/local/lib/libopus.so && \
    ln -sfn "$SODIUM_PATH" /usr/local/lib/libsodium.so

# libdave
COPY --from=libdave /out/lib/ /usr/local/lib/

RUN ldconfig

# yt-dlp
COPY --from=yt-dlp /out /usr/local/bin/yt-dlp

COPY --from=denoland/deno:bin-2.9.8@sha256:b507583d3c68ba69f3ed2ce5969ae5b8b70f43421c781279c7aea06435263543 /deno /usr/local/bin/deno

RUN deno --version

# Application
WORKDIR /app

COPY --from=dotnet-build /app/publish .

USER app:app

CMD ["dotnet", "Bot.dll"]
