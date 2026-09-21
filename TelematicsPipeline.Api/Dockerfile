# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY TelematicsPipeline.Api/TelematicsPipeline.Api.csproj TelematicsPipeline.Api/
RUN dotnet restore TelematicsPipeline.Api/TelematicsPipeline.Api.csproj

COPY TelematicsPipeline.Api/ TelematicsPipeline.Api/
RUN dotnet publish TelematicsPipeline.Api/TelematicsPipeline.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:5231
EXPOSE 5231
ENTRYPOINT ["dotnet", "TelematicsPipeline.Api.dll"]
