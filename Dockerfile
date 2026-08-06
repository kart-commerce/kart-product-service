# Build context is the PARENT directory (kart-commerce/), not this repo alone - see
# docker-compose.yml's `build.context: ..`. This is required because Kart.Shared.Domain/
# Kart.Shared.ErrorHandling/Kart.Shared.Observability are consumed via ProjectReference to the
# sibling kart-shared checkout (no published NuGet feed exists yet - kart-shared's own README
# documents this as the interim consumption path). Once kart-shared publishes real NuGet packages,
# this reverts to a normal single-repo build context with PackageReferences instead.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY kart-product-service/KartProductService.sln kart-product-service/
COPY kart-product-service/Directory.Build.props kart-product-service/
COPY kart-shared/Directory.Build.props kart-shared/
COPY kart-product-service/src/Api/Kart.Product.Api.csproj kart-product-service/src/Api/
COPY kart-product-service/src/Application/Kart.Product.Application.csproj kart-product-service/src/Application/
COPY kart-product-service/src/Domain/Kart.Product.Domain.csproj kart-product-service/src/Domain/
COPY kart-product-service/src/Infrastructure/Kart.Product.Infrastructure.csproj kart-product-service/src/Infrastructure/
COPY kart-product-service/tests/UnitTests/Kart.Product.UnitTests.csproj kart-product-service/tests/UnitTests/
COPY kart-product-service/tests/IntegrationTests/Kart.Product.IntegrationTests.csproj kart-product-service/tests/IntegrationTests/
COPY kart-product-service/tests/ContractTests/Kart.Product.ContractTests.csproj kart-product-service/tests/ContractTests/
COPY kart-shared/src/Kart.Shared.Domain/Kart.Shared.Domain.csproj kart-shared/src/Kart.Shared.Domain/
COPY kart-shared/src/Kart.Shared.ErrorHandling/Kart.Shared.ErrorHandling.csproj kart-shared/src/Kart.Shared.ErrorHandling/
COPY kart-shared/src/Kart.Shared.Observability/Kart.Shared.Observability.csproj kart-shared/src/Kart.Shared.Observability/
COPY kart-shared/src/Kart.Shared.Auditing/Kart.Shared.Auditing.csproj kart-shared/src/Kart.Shared.Auditing/
COPY kart-shared/src/Kart.Shared.Configuration/Kart.Shared.Configuration.csproj kart-shared/src/Kart.Shared.Configuration/
COPY kart-shared/src/Kart.Shared.Messaging/Kart.Shared.Messaging.csproj kart-shared/src/Kart.Shared.Messaging/
RUN dotnet restore kart-product-service/src/Api/Kart.Product.Api.csproj

COPY kart-product-service/ kart-product-service/
COPY kart-shared/ kart-shared/
RUN dotnet publish kart-product-service/src/Api/Kart.Product.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Kart.Product.Api.dll"]
