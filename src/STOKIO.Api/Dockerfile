FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["src/STOKIO.Domain/STOKIO.Domain.csproj", "src/STOKIO.Domain/"]
COPY ["src/STOKIO.Application/STOKIO.Application.csproj", "src/STOKIO.Application/"]
COPY ["src/STOKIO.Infrastructure/STOKIO.Infrastructure.csproj", "src/STOKIO.Infrastructure/"]
COPY ["src/STOKIO.Api/STOKIO.Api.csproj", "src/STOKIO.Api/"]
RUN dotnet restore "src/STOKIO.Api/STOKIO.Api.csproj"
COPY . .
RUN dotnet publish "src/STOKIO.Api/STOKIO.Api.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
ARG APP_UID=1654
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0 \
    Exports__StoragePath=/var/lib/stokio/export-files \
    STOKIO_HEALTHCHECK_URL=http://127.0.0.1:8080/health/ready
WORKDIR /app
RUN mkdir -p /var/lib/stokio/export-files && chown -R ${APP_UID}:0 /var/lib/stokio
COPY --from=build /app/publish .
USER ${APP_UID}
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 CMD ["dotnet", "STOKIO.Api.dll", "--container-healthcheck"]
ENTRYPOINT ["dotnet", "STOKIO.Api.dll"]
