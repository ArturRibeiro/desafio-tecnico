# Contexto de build: raiz do repositório (precisa de Directory.*.props).
#   docker build -f src/Posicao/Posicao.Worker/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/ src/
RUN dotnet publish src/Posicao/Posicao.Worker/Posicao.Worker.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# curl só para o healthcheck do compose, que consulta /health/ready sem publicar a porta do serviço.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Posicao.Worker.dll"]
