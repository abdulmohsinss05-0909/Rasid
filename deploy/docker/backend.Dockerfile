# syntax=docker/dockerfile:1
# One build, two runtime images: `--target api` and `--target worker`.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached unless project files change)
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/backend/Rasid.Domain/Rasid.Domain.csproj src/backend/Rasid.Domain/
COPY src/backend/Rasid.Application/Rasid.Application.csproj src/backend/Rasid.Application/
COPY src/backend/Rasid.Infrastructure/Rasid.Infrastructure.csproj src/backend/Rasid.Infrastructure/
COPY src/backend/Rasid.Api/Rasid.Api.csproj src/backend/Rasid.Api/
COPY src/backend/Rasid.Worker/Rasid.Worker.csproj src/backend/Rasid.Worker/
RUN dotnet restore src/backend/Rasid.Api/Rasid.Api.csproj \
 && dotnet restore src/backend/Rasid.Worker/Rasid.Worker.csproj

COPY src/backend/ src/backend/
RUN dotnet publish src/backend/Rasid.Api/Rasid.Api.csproj -c Release -o /out/api --no-restore /p:UseAppHost=false \
 && dotnet publish src/backend/Rasid.Worker/Rasid.Worker.csproj -c Release -o /out/worker --no-restore /p:UseAppHost=false

# Runtime: ASP.NET Core only (no SDK, no sources), non-root user provided by the base image.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_gcServer=0
EXPOSE 8080
USER $APP_UID

FROM runtime AS api
COPY --from=build /out/api .
ENTRYPOINT ["dotnet", "Rasid.Api.dll"]

FROM runtime AS worker
COPY --from=build /out/worker .
ENTRYPOINT ["dotnet", "Rasid.Worker.dll"]
