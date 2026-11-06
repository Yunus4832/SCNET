FROM mcr.microsoft.com/dotnet/runtime:10.0

ARG GAME_VERSION
ARG SOURCE_REVISION

LABEL org.opencontainers.image.title="SCNET game server" \
      org.opencontainers.image.version=$GAME_VERSION \
      org.opencontainers.image.revision=$SOURCE_REVISION

WORKDIR /app
COPY . .
RUN chmod +x /app/Survivalcraft.Server && mkdir -p /app/Instances

VOLUME ["/app/Instances"]
ENTRYPOINT ["/app/Survivalcraft.Server"]
CMD ["--instance", "server"]
