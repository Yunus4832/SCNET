FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY Directory.Build.targets ./
COPY ModServer/ModServer.csproj ModServer/
RUN dotnet restore ModServer/ModServer.csproj -v minimal

COPY ModServer/ ModServer/
RUN dotnet publish ModServer/ModServer.csproj -c Release -o /app/publish --no-restore -v minimal

FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080

COPY --from=build /app/publish/ ./

EXPOSE 8080

ENTRYPOINT ["dotnet", "ModServer.dll"]
