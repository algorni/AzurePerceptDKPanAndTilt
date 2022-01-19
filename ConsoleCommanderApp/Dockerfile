#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:5.0-buster-slim-arm64v8 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["ConsoleCommanderApp.csproj", "."]
RUN dotnet restore "./ConsoleCommanderApp.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "ConsoleCommanderApp.csproj" -c Release -o /app/build -r linux-arm64

FROM build AS publish
RUN dotnet publish "ConsoleCommanderApp.csproj" -c Release -o /app/publish -r linux-arm64

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConsoleCommanderApp.dll"]