FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /workspace

COPY ["Directory.Packages.props", "."]

COPY ["src/BankingSystem.Api/BankingSystem.Api.csproj", "src/BankingSystem.Api/"]
COPY ["src/BankingSystem.Application/BankingSystem.Application.csproj", "src/BankingSystem.Application/"]
COPY ["src/BankingSystem.Domain/BankingSystem.Domain.csproj", "src/BankingSystem.Domain/"]
COPY ["src/BankingSystem.Infrastructure/BankingSystem.Infrastructure.csproj", "src/BankingSystem.Infrastructure/"]

COPY ["tests/BankingSystem.UnitTests/BankingSystem.UnitTests.csproj", "tests/BankingSystem.UnitTests/"]
COPY ["tests/BankingSystem.IntegrationTests/BankingSystem.IntegrationTests.csproj", "tests/BankingSystem.IntegrationTests/"]

RUN dotnet restore "src/BankingSystem.Api/BankingSystem.Api.csproj" && \
    dotnet restore "tests/BankingSystem.UnitTests/BankingSystem.UnitTests.csproj" && \
    dotnet restore "tests/BankingSystem.IntegrationTests/BankingSystem.IntegrationTests.csproj"

COPY src/ src/
COPY tests/ tests/

FROM restore AS test

RUN dotnet test "tests/BankingSystem.UnitTests/BankingSystem.UnitTests.csproj" \
        -c Release --no-restore --logger "console;verbosity=normal" && \
    dotnet test "tests/BankingSystem.IntegrationTests/BankingSystem.IntegrationTests.csproj" \
        -c Release --no-restore --logger "console;verbosity=normal"

FROM test AS publish

RUN dotnet publish "src/BankingSystem.Api/BankingSystem.Api.csproj" \
    -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN groupadd --system appgroup && useradd --system --gid appgroup appuser

COPY --from=publish --chown=appuser:appgroup /app/publish .

USER appuser

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "BankingSystem.Api.dll"]