FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Fisga.Domain/Fisga.Domain.csproj src/Fisga.Domain/
COPY src/Fisga.Application/Fisga.Application.csproj src/Fisga.Application/
COPY src/Fisga.Infrastructure/Fisga.Infrastructure.csproj src/Fisga.Infrastructure/
COPY src/Fisga.Api/Fisga.Api.csproj src/Fisga.Api/
RUN dotnet restore src/Fisga.Api/Fisga.Api.csproj

COPY src/ src/
RUN dotnet publish src/Fisga.Api/Fisga.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
ARG GIT_SHA=
ARG SOURCE_COMMIT=
ENV GIT_SHA=${GIT_SHA:-$SOURCE_COMMIT}
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Fisga.Api.dll"]
