FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY events.csproj ./
RUN dotnet restore events.csproj
COPY . ./
RUN dotnet publish events.csproj -c Release -o /out --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out ./
ENV PORT=8082
EXPOSE 8082
USER $APP_UID
ENTRYPOINT ["dotnet", "events.dll"]
