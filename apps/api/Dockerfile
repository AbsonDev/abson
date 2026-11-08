FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Abson.Api.csproj ./
RUN dotnet restore Abson.Api.csproj

COPY . ./
RUN dotnet publish Abson.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=build /app/publish ./
USER app

ENTRYPOINT ["dotnet", "Abson.Api.dll"]
