# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/BacchusLedger/BacchusLedger.csproj src/BacchusLedger/
RUN dotnet restore src/BacchusLedger/BacchusLedger.csproj
COPY src/ src/
RUN dotnet publish src/BacchusLedger/BacchusLedger.csproj -c Release -o /app --no-restore

# ---- run ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DB_PATH=/data/bacchus.db \
    RESET_ON_START=false
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "BacchusLedger.dll"]
