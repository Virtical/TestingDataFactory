FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["backend/TestingDataFactory.SpecificationWorker/TestingDataFactory.SpecificationWorker.csproj", "backend/TestingDataFactory.SpecificationWorker/"]
RUN dotnet restore "backend/TestingDataFactory.SpecificationWorker/TestingDataFactory.SpecificationWorker.csproj"
COPY . .
WORKDIR "/src/backend/TestingDataFactory.SpecificationWorker"
RUN dotnet build "./TestingDataFactory.SpecificationWorker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./TestingDataFactory.SpecificationWorker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TestingDataFactory.SpecificationWorker.dll"]
