FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["LBL_Downloader.csproj", "."]
RUN dotnet restore "./LBL_Downloader.csproj"
COPY . .
RUN dotnet build "LBL_Downloader.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "LBL_Downloader.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN apt-get update && apt-get install -y ffmpeg python3 python3-pip && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

ENTRYPOINT ["dotnet", "LBL_Downloader.dll"]