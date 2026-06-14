# syntax=docker/dockerfile:1

# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first for better layer caching.
COPY nuget.config Directory.Build.props ./
COPY src/Platform.Contracts/Platform.Contracts.csproj      src/Platform.Contracts/
COPY src/Platform.Domain/Platform.Domain.csproj            src/Platform.Domain/
COPY src/Platform.Application/Platform.Application.csproj   src/Platform.Application/
COPY src/Platform.Infrastructure/Platform.Infrastructure.csproj src/Platform.Infrastructure/
COPY src/Platform.Realtime/Platform.Realtime.csproj        src/Platform.Realtime/
COPY src/Platform.Api/Platform.Api.csproj                  src/Platform.Api/
RUN dotnet restore src/Platform.Api/Platform.Api.csproj

# Copy the rest and publish.
COPY . .
RUN dotnet publish src/Platform.Api/Platform.Api.csproj -c Release -o /app --no-restore

# --- Runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Bundle the Godot SDK addon so the API can package it for the one-line installer at startup.
COPY --from=build /src/sdk/godot-gdscript/addons/multiplayer_service ./sdk-src/addons/multiplayer_service

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget -qO- http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Platform.Api.dll"]
