# API ASP.NET Core 8 — se construye con el contexto en la raíz del repositorio:
#   docker build -f src/GestionPedidos.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY seed ./seed
COPY src/GestionPedidos.Domain/*.csproj src/GestionPedidos.Domain/
COPY src/GestionPedidos.Contracts/*.csproj src/GestionPedidos.Contracts/
COPY src/GestionPedidos.Application/*.csproj src/GestionPedidos.Application/
COPY src/GestionPedidos.Infrastructure/*.csproj src/GestionPedidos.Infrastructure/
COPY src/GestionPedidos.Api/*.csproj src/GestionPedidos.Api/
RUN dotnet restore src/GestionPedidos.Api/GestionPedidos.Api.csproj
COPY src/GestionPedidos.Domain src/GestionPedidos.Domain
COPY src/GestionPedidos.Contracts src/GestionPedidos.Contracts
COPY src/GestionPedidos.Application src/GestionPedidos.Application
COPY src/GestionPedidos.Infrastructure src/GestionPedidos.Infrastructure
COPY src/GestionPedidos.Api src/GestionPedidos.Api
RUN dotnet publish src/GestionPedidos.Api/GestionPedidos.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "GestionPedidos.Api.dll"]
