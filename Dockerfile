FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY CacheConsistency.sln ./
COPY src/CacheConsistency.Core/CacheConsistency.Core.csproj src/CacheConsistency.Core/
COPY src/CacheConsistency.Infrastructure/CacheConsistency.Infrastructure.csproj src/CacheConsistency.Infrastructure/
COPY src/CacheConsistency.Benchmarks/CacheConsistency.Benchmarks.csproj src/CacheConsistency.Benchmarks/
RUN dotnet restore src/CacheConsistency.Benchmarks/CacheConsistency.Benchmarks.csproj

COPY src/ src/
RUN dotnet publish src/CacheConsistency.Benchmarks/CacheConsistency.Benchmarks.csproj \
    -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "CacheConsistency.Benchmarks.dll"]
