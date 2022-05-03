#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Deckbot.Console/Deckbot.Console.csproj", "Deckbot.Console/"]
RUN dotnet restore "Deckbot.Console/Deckbot.Console.csproj"
COPY . .
WORKDIR "/src/Deckbot.Console"
RUN dotnet build "Deckbot.Console.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Deckbot.Console.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Deckbot.Console.dll"]