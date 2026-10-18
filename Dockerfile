FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project files only, so source edits do not invalidate the package layer.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/MarketData.Domain/*.csproj src/MarketData.Domain/
COPY src/MarketData.Application/*.csproj src/MarketData.Application/
COPY src/MarketData.Infrastructure/*.csproj src/MarketData.Infrastructure/
COPY src/MarketData.Api.Contracts/*.csproj src/MarketData.Api.Contracts/
COPY src/MarketData.Api/*.csproj src/MarketData.Api/
RUN dotnet restore src/MarketData.Api

COPY .editorconfig ./
COPY src/ src/
RUN dotnet publish src/MarketData.Api --no-restore -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
# The ASP.NET image listens on plain HTTP port 8080; TLS ends at the ingress.
ENTRYPOINT ["dotnet", "MarketData.Api.dll"]
