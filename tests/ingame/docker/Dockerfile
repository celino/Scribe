# Test-only image: runs the KSP Linux player headless under Xvfb with software GL.
# It contains NO game files and NO Unity editor; KSP is mounted from a volume you own
# (read-only, through an overlay; see tools/run-ingame.sh). The image is never published
# to a registry: build it locally (`docker build -t scribe-ksp-test tests/ingame/docker`).
# The .NET SDK is included only to build the plugin and the round driver in the same container.
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim

RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      xvfb xauth libgl1 libglu1-mesa libgl1-mesa-dri libasound2 libxcursor1 libxrandr2 libxinerama1 libxi6 \
      libgtk2.0-0 libglib2.0-0 libpulse0 ca-certificates \
 && rm -rf /var/lib/apt/lists/*

# Same uid as the host user in most setups; the overlay's upper layer is chowned to it.
RUN useradd -m -u 1000 ksp
USER ksp
ENV HOME=/home/ksp LIBGL_ALWAYS_SOFTWARE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
WORKDIR /game
