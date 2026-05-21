FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ClaudeMcpServer.LicenseServer.csproj", "."]
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Persist the SQLite database across restarts via a mounted volume.
# On Railway/Render: mount a volume at /app/data and set:
#   ConnectionStrings__DefaultConnection=Data Source=/app/data/licenses.db
VOLUME ["/app/data"]

ENTRYPOINT ["dotnet", "ClaudeMcpServer.LicenseServer.dll"]
