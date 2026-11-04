# Multi-stage build (ver docs/compliance/production-config.md) — a imagem final não carrega
# o SDK do .NET, só o runtime, e roda como usuário não-root. O Worker não expõe HTTP; a
# "saúde" dele é a presença do processo dotnet e o heartbeat do servidor Hangfire (ver
# HealthCheckJob/logs — sem endpoint HTTP, não há /health/live para checar aqui).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props Directory.Build.props* ./
COPY Advocacia.sln ./
COPY src/ src/

RUN dotnet restore src/Hosts/Worker/Worker.csproj
RUN dotnet publish src/Hosts/Worker/Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

RUN groupadd --system --gid 1500 advocacia \
    && useradd --system --uid 1500 --gid advocacia --shell /usr/sbin/nologin advocacia

COPY --from=build /app/publish .
RUN chown -R advocacia:advocacia /app
USER advocacia

ENTRYPOINT ["dotnet", "Worker.dll"]
