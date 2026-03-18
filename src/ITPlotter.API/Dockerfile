FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update && apt-get install -y --no-install-recommends ghostscript cups-client && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/ITPlotter.API/ITPlotter.API.csproj", "src/ITPlotter.API/"]
COPY ["src/ITPlotter.Application/ITPlotter.Application.csproj", "src/ITPlotter.Application/"]
COPY ["src/ITPlotter.Domain/ITPlotter.Domain.csproj", "src/ITPlotter.Domain/"]
COPY ["src/ITPlotter.Infrastructure/ITPlotter.Infrastructure.csproj", "src/ITPlotter.Infrastructure/"]
RUN dotnet restore "src/ITPlotter.API/ITPlotter.API.csproj"
COPY . .
WORKDIR "/src/src/ITPlotter.API"
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ITPlotter.API.dll"]
