FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY src/UnifyMcp.csproj src/
RUN dotnet restore src/UnifyMcp.csproj
COPY src/ src/
RUN dotnet publish src/UnifyMcp.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --create-home --shell /usr/sbin/nologin unify

USER unify
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "UnifyMcp.dll"]
