# Build from the repository root:  docker build -f BodyOps.WebApi/Dockerfile -t bodyops .

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY BodyOps.Library/BodyOps.Library.csproj BodyOps.Library/
COPY BodyOps.WebApi/BodyOps.WebApi.csproj BodyOps.WebApi/
RUN dotnet restore BodyOps.WebApi/BodyOps.WebApi.csproj
COPY BodyOps.Library/ BodyOps.Library/
COPY BodyOps.WebApi/ BodyOps.WebApi/
# Files copied from a NAS share can keep owner-only permissions; the app runs as the non-root "app" user.
RUN dotnet publish BodyOps.WebApi/BodyOps.WebApi.csproj -c Release -o /app --no-restore \
    && chmod -R a+rX /app

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    BodyOps__DataDirectory=/data

# The aspnet image runs as the non-root "app" user (UID in $APP_UID); give it the data folder (login keys).
RUN mkdir -p /data && chown -R $APP_UID /data
VOLUME /data

COPY --from=build /app .

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "BodyOps.WebApi.dll"]
