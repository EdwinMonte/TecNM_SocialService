# Etapa 1: Build de la aplicación .NET 10
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY ["TecNM.Api.csproj", "./"]
RUN dotnet restore "TecNM.Api.csproj"

COPY . .
RUN dotnet publish "TecNM.Api.csproj" -c Release -o /app/publish --no-restore

# Etapa 2: Runtime ligero
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

RUN apk add --no-cache icu-libs icu-data-full tzdata

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "TecNM.Api.dll"]
