FROM mcr.microsoft.com/dotnet/runtime:10.0

ARG SCNET_VERSION
ARG SOURCE_REVISION

LABEL org.opencontainers.image.title="SCNET game server" \
      org.opencontainers.image.version=$SCNET_VERSION \
      org.opencontainers.image.revision=$SOURCE_REVISION

WORKDIR /app
COPY . .
RUN chmod +x /app/SurvivalcraftStarter && mkdir -p /app/Instances

VOLUME ["/app/Instances"]
ENTRYPOINT ["/app/SurvivalcraftStarter"]
CMD ["--instance", "server", "--server"]
