FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled

ARG BUILD_DATE
ARG RENDEZVOUS_SERVER_VERSION
ARG SCNET_VERSION
ARG SOURCE_REVISION

LABEL org.opencontainers.image.created=$BUILD_DATE \
      org.opencontainers.image.description="SCNET signaling and authenticated UDP relay" \
      org.opencontainers.image.revision=$SOURCE_REVISION \
      org.opencontainers.image.title="SCNET RendezvousServer" \
      org.opencontainers.image.version=$RENDEZVOUS_SERVER_VERSION \
      org.scnet.network.version=$SCNET_VERSION

WORKDIR /app
COPY . .
ENTRYPOINT ["dotnet", "RendezvousServer.dll"]
