FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish src/PLCFlow.Cli/PLCFlow.Cli.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime:8.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends clang lld nodejs \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out ./
COPY examples ./examples
ENTRYPOINT ["dotnet", "PLCFlow.Cli.dll"]
