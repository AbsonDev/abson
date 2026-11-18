FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

COPY Directory.Build.props global.json ./
COPY src/Fisga.Domain/Fisga.Domain.csproj src/Fisga.Domain/packages.lock.json src/Fisga.Domain/
COPY src/Fisga.Application/Fisga.Application.csproj src/Fisga.Application/packages.lock.json src/Fisga.Application/
COPY src/Fisga.Infrastructure/Fisga.Infrastructure.csproj src/Fisga.Infrastructure/packages.lock.json src/Fisga.Infrastructure/
COPY src/Fisga.Api/Fisga.Api.csproj src/Fisga.Api/packages.lock.json src/Fisga.Api/
RUN dotnet restore src/Fisga.Api/Fisga.Api.csproj --locked-mode

COPY src/ src/
RUN dotnet publish src/Fisga.Api/Fisga.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS final
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
