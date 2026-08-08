# syntax=docker/dockerfile:1
#
# LinguaTalk — ASP.NET Core MVC + SQLite (Render.com)
# Docker build context = repository root (this file's directory).
#
# Repo layout (relative to context):
#   src/LanguagePracticePlatform.sln
#   src/LanguagePractice.Core/
#   src/LanguagePractice.Infrastructure/
#   src/LanguagePractice.Web/

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

ENV DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    NUGET_XMLDOC_MODE=skip

# --- restore layer (csproj + sln only) ---
COPY src/LanguagePracticePlatform.sln ./
COPY src/LanguagePractice.Core/LanguagePractice.Core.csproj LanguagePractice.Core/
COPY src/LanguagePractice.Infrastructure/LanguagePractice.Infrastructure.csproj LanguagePractice.Infrastructure/
COPY src/LanguagePractice.Web/LanguagePractice.Web.csproj LanguagePractice.Web/

RUN dotnet restore ./LanguagePracticePlatform.sln

# --- full sources for the three .NET projects ---
COPY src/LanguagePractice.Core/ LanguagePractice.Core/
COPY src/LanguagePractice.Infrastructure/ LanguagePractice.Infrastructure/
COPY src/LanguagePractice.Web/ LanguagePractice.Web/

# Publish without --no-restore so assets stay consistent after source copy
RUN dotnet publish ./LanguagePractice.Web/LanguagePractice.Web.csproj \
    -c Release \
    -o /app/publish \
    --verbosity normal \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

RUN mkdir -p /data /app/App_Data /app/wwwroot/uploads/avatars \
    && chmod -R 777 /data /app/App_Data /app/wwwroot/uploads

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://0.0.0.0:8080 \
    Database__Provider=Sqlite \
    ConnectionStrings__Sqlite="Data Source=/data/linguatalk.db" \
    ConnectionStrings__DefaultConnection="Data Source=/data/linguatalk.db" \
    Signaling__PublicUrl=https://l-nguatalk-1.onrender.com \
    Moderation__SignalingModerationKey=dev-moderation-key \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_NOLOGO=1 \
    DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false \
    HostBuilder__ReloadConfigOnChange=false \
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    DISABLE_FILE_WATCHERS=true

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["sh", "-c", "export ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080}; exec dotnet LanguagePractice.Web.dll"]
