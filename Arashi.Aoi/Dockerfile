#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["Arashi.Aoi/Arashi.Aoi.csproj", "Arashi.Aoi/"]
RUN dotnet restore "Arashi.Aoi/Arashi.Aoi.csproj"
COPY . .
WORKDIR "/src/Arashi.Aoi"
RUN dotnet build "Arashi.Aoi.csproj" -c Release -o /app/build /p:PublishSingleFile=false /p:PublishTrimmed=false

FROM build AS publish
RUN dotnet publish "Arashi.Aoi.csproj" -c Release -o /app/publish /p:PublishSingleFile=false /p:PublishTrimmed=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 2020
#ENTRYPOINT ["dotnet", "Arashi.Aoi.dll"]
CMD ASPNETCORE_URLS=http://*:$PORT dotnet Arashi.Aoi.dll