FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled

ARG BUILD_DATE
ARG CONTENT_SERVER_VERSION
ARG SCNET_VERSION
ARG SOURCE_REVISION

LABEL org.opencontainers.image.created=$BUILD_DATE \
      org.opencontainers.image.description="SCNET ContentServer" \
      org.opencontainers.image.revision=$SOURCE_REVISION \
      org.opencontainers.image.title="SCNET ContentServer" \
      org.opencontainers.image.version=$CONTENT_SERVER_VERSION \
      org.scnet.network.version=$SCNET_VERSION

ENV ASPNETCORE_URLS=http://+:8080 \
    ContentServer__DatabasePath=/data/content-server.db \
    ContentServer__PackageStoragePath=/data

WORKDIR /app
COPY . .

EXPOSE 8080
VOLUME ["/data"]

USER 0
ENTRYPOINT ["dotnet", "ContentServer.dll"]
