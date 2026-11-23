# Web (Blazor Web App) — contexto en la raíz del repositorio:
#   docker build -f src/GestionPedidos.Web/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/GestionPedidos.Contracts/*.csproj src/GestionPedidos.Contracts/
COPY src/GestionPedidos.Ui/*.csproj src/GestionPedidos.Ui/
COPY src/GestionPedidos.Web/*.csproj src/GestionPedidos.Web/
RUN dotnet restore src/GestionPedidos.Web/GestionPedidos.Web.csproj
COPY src/GestionPedidos.Contracts src/GestionPedidos.Contracts
COPY src/GestionPedidos.Ui src/GestionPedidos.Ui
COPY src/GestionPedidos.Web src/GestionPedidos.Web
# Sin --no-restore: con el SDK 10 el restore hecho solo con los .csproj no incluye los activos de Blazor (_framework/blazor.web.js)
# y la web quedaba sin interactividad. El restore de arriba sigue cacheando la descarga de paquetes.
RUN dotnet publish src/GestionPedidos.Web/GestionPedidos.Web.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "GestionPedidos.Web.dll"]
