# Мультистейдж-сборка: в финальный образ уезжает только опубликованное приложение.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Сначала копирую только csproj — так слой с restore переиспользуется, пока не менялись зависимости.
COPY CalorieBot.sln ./
COPY src/CalorieBot.Api/CalorieBot.Api.csproj src/CalorieBot.Api/
COPY src/CalorieBot.Core/CalorieBot.Core.csproj src/CalorieBot.Core/
COPY src/CalorieBot.Data/CalorieBot.Data.csproj src/CalorieBot.Data/
RUN dotnet restore src/CalorieBot.Api/CalorieBot.Api.csproj

# А теперь исходники и публикация.
COPY src/ src/
RUN dotnet publish src/CalorieBot.Api/CalorieBot.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# curl нужен для HEALTHCHECK — в базовом образе его нет.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=0

# Работаю под встроенным непривилегированным пользователем образа.
USER app
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "CalorieBot.Api.dll"]
