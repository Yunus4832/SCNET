FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled

ARG BUILD_DATE
ARG AUTHENTICATION_SERVER_VERSION
ARG SOURCE_REVISION

LABEL org.opencontainers.image.created=$BUILD_DATE \
      org.opencontainers.image.description="SCNET authentication protocol reference provider" \
      org.opencontainers.image.revision=$SOURCE_REVISION \
      org.opencontainers.image.title="SCNET Authentication Server" \
      org.opencontainers.image.version=$AUTHENTICATION_SERVER_VERSION

ENV ASPNETCORE_URLS=http://+:8080 \
    Authentication__DataDirectory=/home/app/AuthenticationData

WORKDIR /app
COPY . .
EXPOSE 8080
VOLUME ["/home/app"]
ENTRYPOINT ["dotnet", "Authentication.Server.dll"]
