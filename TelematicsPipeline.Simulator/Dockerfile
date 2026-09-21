# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY TelematicsPipeline.Simulator/TelematicsPipeline.Simulator.csproj TelematicsPipeline.Simulator/
RUN dotnet restore TelematicsPipeline.Simulator/TelematicsPipeline.Simulator.csproj

COPY TelematicsPipeline.Simulator/ TelematicsPipeline.Simulator/
RUN dotnet publish TelematicsPipeline.Simulator/TelematicsPipeline.Simulator.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "TelematicsPipeline.Simulator.dll"]
