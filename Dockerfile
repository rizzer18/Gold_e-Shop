# Базовий образ для запуску (runtime)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Образ для збірки (SDK)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Копіюємо тільки csproj і робимо restore
COPY ["Gold_e-Shop.csproj", "./"]
RUN dotnet restore "Gold_e-Shop.csproj"

# Копіюємо весь проект і збираємо
COPY . .
RUN dotnet build "Gold_e-Shop.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Публікація
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Gold_e-Shop.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Фінальний образ
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Gold_e-Shop.dll"]
