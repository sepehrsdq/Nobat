# Single image: the Vue build lands in wwwroot, then the API serves both.
# Build from the repository root:  docker build -t nobat .
# Run:  docker run --rm -p 8080:8080 -e ConnectionStrings__Default="..." nobat

# ---- 1. frontend ----
# bookworm (glibc), not alpine: the lockfile and CI are built against glibc, and this stage
# only produces static files, so image size does not matter.
FROM node:24-bookworm-slim AS web

WORKDIR /src

COPY web/package.json web/package-lock.json ./web/
RUN cd web && npm ci

COPY web/ ./web/
# Vite writes to ../src/Nobat.Api/wwwroot, so the source tree must exist.
COPY src/Nobat.Api/Nobat.Api.csproj ./src/Nobat.Api/
RUN cd web && npm run build

# ---- 2. backend ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# global.json is deliberately not copied: it pins the local SDK band (10.0.3xx) and the
# image's own 10.0 SDK is the one that must build this.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ ./src/
COPY --from=web /src/src/Nobat.Api/wwwroot ./src/Nobat.Api/wwwroot

RUN dotnet restore src/Nobat.Api/Nobat.Api.csproj
RUN dotnet publish src/Nobat.Api/Nobat.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---- 3. runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish ./

EXPOSE 8080

# The aspnet image ships this non-root user.
USER $APP_UID

ENTRYPOINT ["dotnet", "Nobat.Api.dll"]
