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
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "STOKIO.Api.dll"]
