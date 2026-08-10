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
ARG YTDLP_VERSION=2026.07.04

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
    && rm -rf /var/lib/apt/lists/*

RUN case "${TARGETARCH}" in \
        amd64)  YTDLP_ARCH="linux" ;; \
        arm64)  YTDLP_ARCH="linux_aarch64" ;; \
        *) echo "Unsupported architecture: ${TARGETARCH}" && exit 1 ;; \
    esac && \
    curl -fL \
        "https://github.com/yt-dlp/yt-dlp/releases/download/${YTDLP_VERSION}/yt-dlp_${YTDLP_ARCH}" \
        -o /yt-dlp && \
    chmod 755 /yt-dlp


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

# yt-dlp
COPY --from=yt-dlp /yt-dlp /usr/local/bin/yt-dlp

RUN ldconfig

# Application
WORKDIR /app

COPY --from=dotnet-build /app/publish .


CMD ["dotnet", "Bot.dll"]