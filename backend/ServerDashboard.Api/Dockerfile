FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ServerDashboard.Api.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# lm-sensors for CPU temperature (SystemMetricsService shells out to `sensors -j`,
# the same way GpuMetricsService shells out to nvidia-smi).
RUN apt-get update \
    && apt-get install -y --no-install-recommends lm-sensors \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5035
EXPOSE 5035

ENTRYPOINT ["dotnet", "ServerDashboard.Api.dll"]
