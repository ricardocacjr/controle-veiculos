# Build context: raiz do repositório (não a pasta deste projeto).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/ControleVeiculos.Web/ControleVeiculos.Web.csproj", "src/ControleVeiculos.Web/"]
COPY ["src/ControleVeiculos.Shared/ControleVeiculos.Shared.csproj", "src/ControleVeiculos.Shared/"]
COPY ["src/ControleVeiculos.Domain/ControleVeiculos.Domain.csproj", "src/ControleVeiculos.Domain/"]
RUN dotnet restore "src/ControleVeiculos.Web/ControleVeiculos.Web.csproj"

COPY src/ControleVeiculos.Web/ src/ControleVeiculos.Web/
COPY src/ControleVeiculos.Shared/ src/ControleVeiculos.Shared/
COPY src/ControleVeiculos.Domain/ src/ControleVeiculos.Domain/

RUN dotnet publish "src/ControleVeiculos.Web/ControleVeiculos.Web.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
# Containers com limite baixo de inotify (comum em planos free de PaaS) derrubam o app na
# inicialização porque o .NET tenta observar appsettings.json pra hot-reload. Desativa isso.
ENV DOTNET_hostBuilder__reloadConfigOnChange=false
EXPOSE 8080

ENTRYPOINT ["dotnet", "ControleVeiculos.Web.dll"]
