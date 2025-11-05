# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app ./

# Environment defaults (можно переопределить)
ENV ASPNETCORE_URLS=http://*:8080
ENV DOTNET_ENVIRONMENT=Production
ENV DATA_SOURCE="Data Source=/app/data/starter.db"
ENV JWT_SECRET=""
ENV HMAC_SECRET=""

# Create data dir
RUN mkdir -p /app/data

EXPOSE 8080/tcp

ENTRYPOINT ["dotnet", "Starter.dll"]