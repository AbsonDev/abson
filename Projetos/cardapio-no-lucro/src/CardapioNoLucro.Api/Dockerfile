FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/CardapioNoLucro.Domain/CardapioNoLucro.Domain.csproj src/CardapioNoLucro.Domain/
COPY src/CardapioNoLucro.Application/CardapioNoLucro.Application.csproj src/CardapioNoLucro.Application/
COPY src/CardapioNoLucro.Infrastructure/CardapioNoLucro.Infrastructure.csproj src/CardapioNoLucro.Infrastructure/
COPY src/CardapioNoLucro.Api/CardapioNoLucro.Api.csproj src/CardapioNoLucro.Api/
RUN dotnet restore src/CardapioNoLucro.Api/CardapioNoLucro.Api.csproj

COPY src/ src/
RUN dotnet publish src/CardapioNoLucro.Api/CardapioNoLucro.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CardapioNoLucro.Api.dll"]
