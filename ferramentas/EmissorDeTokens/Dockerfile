# Emissor de tokens SOMENTE PARA DESENVOLVIMENTO. Contexto de build: raiz do repositório.
#   docker compose run --rm emissor-de-tokens --titular cliente-1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY ferramentas/EmissorDeTokens/ ferramentas/EmissorDeTokens/
RUN dotnet publish ferramentas/EmissorDeTokens/EmissorDeTokens.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "EmissorDeTokens.dll"]
