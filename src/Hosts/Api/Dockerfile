# Multi-stage build (ver docs/compliance/production-config.md) — a imagem final não carrega
# o SDK do .NET, só o runtime ASP.NET Core, e roda como usuário não-root.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props Directory.Build.props* ./
COPY Advocacia.sln ./
COPY src/ src/

RUN dotnet restore src/Hosts/Api/Api.csproj
RUN dotnet publish src/Hosts/Api/Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

RUN groupadd --system --gid 1500 advocacia \
    && useradd --system --uid 1500 --gid advocacia --shell /usr/sbin/nologin advocacia

COPY --from=build /app/publish .
RUN chown -R advocacia:advocacia /app
USER advocacia

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Api.dll"]
