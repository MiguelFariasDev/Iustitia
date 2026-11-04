# Imagem do job de migração (Container App Job caj-advocacia-db-migrate-<env>).
# Empacota as migrations do EF Core num "migration bundle" autocontido: a imagem
# final não tem SDK nem dotnet-ef, só o executável que aplica as migrations
# pendentes. A connection string (dono do schema, nunca app_user) chega pela
# variável ADVOCACIA_DB_CONNECTION, lida por PlatformDbContextFactory.
#
# Build (contexto = raiz do repositório):
#   docker build -f infra/docker/migrator.Dockerfile -t advocacia-migrator .
# Rollback para uma migration específica (ver docs/runbooks/database-migration.md):
#   docker run --rm -e ADVOCACIA_DB_CONNECTION=... advocacia-migrator <NomeDaMigration>

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

ARG DOTNET_EF_VERSION=10.0.12
RUN dotnet tool install --global dotnet-ef --version ${DOTNET_EF_VERSION}
ENV PATH="${PATH}:/root/.dotnet/tools"

COPY Directory.Packages.props Directory.Build.props* ./
COPY Advocacia.sln ./
COPY src/ src/

RUN dotnet restore src/Platform/Platform.Infrastructure/Platform.Infrastructure.csproj -r linux-x64
RUN dotnet ef migrations bundle \
      --project src/Platform/Platform.Infrastructure/Platform.Infrastructure.csproj \
      --startup-project src/Platform/Platform.Infrastructure/Platform.Infrastructure.csproj \
      --configuration Release \
      --self-contained \
      --target-runtime linux-x64 \
      --output /app/efbundle

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS final
WORKDIR /app

# libgssapi: o Npgsql tenta carregá-la (autenticação Kerberos) e, sem ela, imprime
# um "Error:" inofensivo que confunde quem lê o log do job.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

RUN groupadd --system --gid 1500 advocacia \
    && useradd --system --uid 1500 --gid advocacia --shell /usr/sbin/nologin advocacia

COPY --from=build --chown=advocacia:advocacia /app/efbundle /app/efbundle
USER advocacia

ENTRYPOINT ["/app/efbundle"]
