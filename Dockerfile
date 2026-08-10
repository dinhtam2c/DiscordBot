# =========================
# 1. Build .NET application
# =========================

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS dotnet-build

WORKDIR /src

COPY . .

RUN dotnet restore

RUN dotnet publish src/Bot/Bot.csproj \
    -c Release \
    -o /app/publish \
    --no-restore


# =========================
# 2. Build libdave
# =========================

FROM ubuntu:24.04 AS dave-build

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        build-essential \
        ca-certificates \
        cmake \
        git \
        pkg-config \
        python3 \
        curl \
        zip \
        unzip \
        tar \
        autoconf \
        automake \
        libtool \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src

RUN git clone --recurse-submodules \
    https://github.com/discord/libdave.git

WORKDIR /src/libdave/cpp

RUN ./vcpkg/bootstrap-vcpkg.sh

RUN make cclean && \
    make shared && \
    make install


# =========================
# 3. Runtime
# =========================
FROM mcr.microsoft.com/dotnet/runtime:10.0.10-noble AS runtime

ARG YTDLP_VERSION=2026.07.04

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ffmpeg \
        libopus0 \
        libsodium23 \
        libssl3 \
        ca-certificates \
        curl \
    && \
    rm -rf /var/lib/apt/lists/*

RUN OPUS_PATH="$(ldconfig -p | awk '/libopus\.so\.0/{print $NF; exit}')" && \
    SODIUM_PATH="$(ldconfig -p | awk '/libsodium\.so\.23/{print $NF; exit}')" && \
    ln -sfn "$OPUS_PATH" /usr/local/lib/libopus.so && \
    ln -sfn "$SODIUM_PATH" /usr/local/lib/libsodium.so && \
    ldconfig

RUN curl -fL \
        "https://github.com/yt-dlp/yt-dlp/releases/download/${YTDLP_VERSION}/yt-dlp_linux" \
        -o /usr/local/bin/yt-dlp && \
    chmod +x /usr/local/bin/yt-dlp

# libdave
COPY --from=dave-build \
    /src/libdave/cpp/build/install/lib/libdave.so* \
    /usr/local/lib/

RUN ldconfig

WORKDIR /app

COPY --from=dotnet-build /app/publish .

ENTRYPOINT ["dotnet", "Bot.dll"]