# LinguaTalk — ASP.NET Core MVC (LanguagePractice.Web) + SQLite
# Render.com: Web Service → Docker, Root Directory = repo kökü
#
# Not: Node.js signaling-service ayrı bir Render Web Service olarak
#     (veya ayrı container) deploy edilmelidir; bu imaj yalnızca .NET web uygulamasını çalıştırır.

# ─── Build ───────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore katmanını önbelleğe almak için önce yalnızca csproj dosyaları
COPY src/LanguagePractice.Core/LanguagePractice.Core.csproj LanguagePractice.Core/
COPY src/LanguagePractice.Infrastructure/LanguagePractice.Infrastructure.csproj LanguagePractice.Infrastructure/
COPY src/LanguagePractice.Web/LanguagePractice.Web.csproj LanguagePractice.Web/

RUN dotnet restore LanguagePractice.Web/LanguagePractice.Web.csproj

# Kaynak kod
COPY src/LanguagePractice.Core/ LanguagePractice.Core/
COPY src/LanguagePractice.Infrastructure/ LanguagePractice.Infrastructure/
COPY src/LanguagePractice.Web/ LanguagePractice.Web/

RUN dotnet publish LanguagePractice.Web/LanguagePractice.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ─── Runtime ─────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

# SQLite ve yüklenen avatarlar için yazılabilir dizinler
RUN mkdir -p /data /app/App_Data /app/wwwroot/uploads/avatars \
    && chmod -R 777 /data /app/App_Data /app/wwwroot/uploads

# Varsayılanlar (Render ortam değişkenleriyle override edilebilir)
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://0.0.0.0:8080 \
    Database__Provider=Sqlite \
    ConnectionStrings__Sqlite="Data Source=/data/linguatalk.db" \
    ConnectionStrings__DefaultConnection="Data Source=/data/linguatalk.db" \
    DOTNET_EnableDiagnostics=0

# Render HTTP health check için; uygulama PORT ile dinler
EXPOSE 8080

COPY --from=build /app/publish .

# Render $PORT enjekte eder; yoksa 8080
ENTRYPOINT ["sh", "-c", "export ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080}; exec dotnet LanguagePractice.Web.dll"]
