# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/BacchusLedger.Domain/BacchusLedger.Domain.csproj src/BacchusLedger.Domain/
COPY src/BacchusLedger.Application/BacchusLedger.Application.csproj src/BacchusLedger.Application/
COPY src/BacchusLedger.Infrastructure/BacchusLedger.Infrastructure.csproj src/BacchusLedger.Infrastructure/
COPY src/BacchusLedger.Api/BacchusLedger.Api.csproj src/BacchusLedger.Api/
RUN dotnet restore src/BacchusLedger.Api/BacchusLedger.Api.csproj
COPY src/ src/
RUN dotnet publish src/BacchusLedger.Api/BacchusLedger.Api.csproj -c Release -o /app --no-restore

# ---- run ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DB_PATH=/data/bacchus.db \
    RESET_ON_START=false
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "BacchusLedger.Api.dll"]
