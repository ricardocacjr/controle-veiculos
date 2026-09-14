# Build context: raiz do repositório (não a pasta deste projeto).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/ControleVeiculos.Api/ControleVeiculos.Api.csproj", "src/ControleVeiculos.Api/"]
COPY ["src/ControleVeiculos.Application/ControleVeiculos.Application.csproj", "src/ControleVeiculos.Application/"]
COPY ["src/ControleVeiculos.Infrastructure/ControleVeiculos.Infrastructure.csproj", "src/ControleVeiculos.Infrastructure/"]
COPY ["src/ControleVeiculos.Domain/ControleVeiculos.Domain.csproj", "src/ControleVeiculos.Domain/"]
COPY ["src/ControleVeiculos.Shared/ControleVeiculos.Shared.csproj", "src/ControleVeiculos.Shared/"]
RUN dotnet restore "src/ControleVeiculos.Api/ControleVeiculos.Api.csproj"

COPY src/ControleVeiculos.Api/ src/ControleVeiculos.Api/
COPY src/ControleVeiculos.Application/ src/ControleVeiculos.Application/
COPY src/ControleVeiculos.Infrastructure/ src/ControleVeiculos.Infrastructure/
COPY src/ControleVeiculos.Domain/ src/ControleVeiculos.Domain/
COPY src/ControleVeiculos.Shared/ src/ControleVeiculos.Shared/

RUN dotnet publish "src/ControleVeiculos.Api/ControleVeiculos.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
# Containers com limite baixo de inotify (comum em planos free de PaaS) derrubam o app na
# inicialização porque o .NET tenta observar appsettings.json pra hot-reload. Desativa isso.
ENV DOTNET_hostBuilder__reloadConfigOnChange=false
EXPOSE 8080

ENTRYPOINT ["dotnet", "ControleVeiculos.Api.dll"]
