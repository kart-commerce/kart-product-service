FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY KartProductService.sln Directory.Build.props nuget.config ./
COPY packages/ packages/
COPY src/Api/Kart.Product.Api.csproj src/Api/
COPY src/Application/Kart.Product.Application.csproj src/Application/
COPY src/Domain/Kart.Product.Domain.csproj src/Domain/
COPY src/Infrastructure/Kart.Product.Infrastructure.csproj src/Infrastructure/
RUN dotnet restore src/Api/Kart.Product.Api.csproj

COPY src/ src/
COPY contracts/ contracts/
RUN dotnet publish src/Api/Kart.Product.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
# kart-infra's helm/service-chart defaults podSecurityContext.runAsNonRoot: true — run as the
# built-in non-root app user so this image deploys under that chart without an override.
USER $APP_UID
ENTRYPOINT ["dotnet", "Kart.Product.Api.dll"]
