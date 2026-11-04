FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["backend/TestingDataFactory.Contracts/TestingDataFactory.Contracts.csproj", "backend/TestingDataFactory.Contracts/"]
COPY ["backend/TestingDataFactory.Worker/TestingDataFactory.Worker.csproj", "backend/TestingDataFactory.Worker/"]
RUN dotnet restore "backend/TestingDataFactory.Worker/TestingDataFactory.Worker.csproj"
COPY . .
WORKDIR "/src/backend/TestingDataFactory.Worker"

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./TestingDataFactory.Worker.csproj" --no-restore -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TestingDataFactory.Worker.dll"]
