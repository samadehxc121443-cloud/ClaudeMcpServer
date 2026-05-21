FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/ClaudeMcpServer.LicenseServer/ClaudeMcpServer.LicenseServer.csproj", "src/ClaudeMcpServer.LicenseServer/"]
RUN dotnet restore "src/ClaudeMcpServer.LicenseServer/ClaudeMcpServer.LicenseServer.csproj"
COPY . .
RUN dotnet publish "src/ClaudeMcpServer.LicenseServer/ClaudeMcpServer.LicenseServer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
VOLUME ["/app/data"]
ENTRYPOINT ["dotnet", "ClaudeMcpServer.LicenseServer.dll"]
