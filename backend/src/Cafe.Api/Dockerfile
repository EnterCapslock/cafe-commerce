# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /src

# Restore first, so this layer is reused until a package changes
COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build-env /app/publish .

# Render and similar hosts set PORT, which Program.cs honors. 8080 is the default.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
EXPOSE 8080

# The base image ships a non-root user. Never run the API as root.
USER $APP_UID

ENTRYPOINT ["dotnet", "Cafe.Api.dll"]