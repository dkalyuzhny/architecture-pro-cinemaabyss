FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY proxy.csproj ./
RUN dotnet restore proxy.csproj
COPY . ./
RUN dotnet publish proxy.csproj -c Release -o /out --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out ./
ENV PORT=8000 DOTNET_USE_POLLING_FILE_WATCHER=1
EXPOSE 8000
USER $APP_UID
ENTRYPOINT ["dotnet", "proxy.dll"]
