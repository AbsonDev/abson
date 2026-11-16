FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Precifica.Domain/Precifica.Domain.csproj src/Precifica.Domain/
COPY src/Precifica.Application/Precifica.Application.csproj src/Precifica.Application/
COPY src/Precifica.Infrastructure/Precifica.Infrastructure.csproj src/Precifica.Infrastructure/
COPY src/Precifica.Api/Precifica.Api.csproj src/Precifica.Api/
RUN dotnet restore src/Precifica.Api/Precifica.Api.csproj

COPY src/ src/
RUN dotnet publish src/Precifica.Api/Precifica.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Precifica.Api.dll"]
