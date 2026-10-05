# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore em camada própria: mudanças de código não invalidam o cache de pacotes.
COPY Directory.Build.props ./
COPY src/Sage.Kahoot.Domain/*.csproj src/Sage.Kahoot.Domain/
COPY src/Sage.Kahoot.Application/*.csproj src/Sage.Kahoot.Application/
COPY src/Sage.Kahoot.Infrastructure/*.csproj src/Sage.Kahoot.Infrastructure/
COPY src/Sage.Kahoot.Api/*.csproj src/Sage.Kahoot.Api/
RUN dotnet restore src/Sage.Kahoot.Api/Sage.Kahoot.Api.csproj

COPY src/ src/
RUN dotnet publish src/Sage.Kahoot.Api/Sage.Kahoot.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Usuário não-root já presente na imagem base.
USER $APP_UID

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

ENTRYPOINT ["dotnet", "Sage.Kahoot.Api.dll"]
