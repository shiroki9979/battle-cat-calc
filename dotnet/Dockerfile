FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS sdk

ARG DEV_UID=1000
ARG DEV_GID=1000
RUN if id ubuntu >/dev/null 2>&1; then userdel --remove ubuntu; fi \
    && groupmod --gid "$DEV_GID" app \
    && usermod --uid "$DEV_UID" --gid "$DEV_GID" app \
    && mkdir -p /workspace/dotnet /workspace/tests/fixtures /artifacts /test-results /home/app/.nuget/packages \
    && chown -R app:app /workspace /artifacts /test-results /home/app

ENV CONTAINER_ARTIFACTS_PATH=/artifacts \
    NUGET_PACKAGES=/home/app/.nuget/packages \
    DOTNET_CLI_HOME=/home/app \
    DOTNET_NOLOGO=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

WORKDIR /workspace/dotnet
COPY --chown=app:app dotnet/ /workspace/dotnet/
COPY --chown=app:app tests/fixtures/ /workspace/tests/fixtures/
USER app
CMD ["sleep", "infinity"]

FROM sdk AS publish
RUN --mount=type=cache,target=/home/app/.nuget/packages,uid=1000,gid=1000 \
    dotnet publish src/BattleCatCalc.Api/BattleCatCalc.Api.csproj \
    --configuration Release --output /artifacts/publish/api -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir /app/logs \
    && chown app:app /app/logs
COPY --from=publish --chown=app:app /artifacts/publish/api/ /app/
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
USER app
EXPOSE 8080
HEALTHCHECK --interval=5s --timeout=3s --start-period=10s --retries=10 \
    CMD curl --fail --silent --show-error http://localhost:8080/api/v1/health || exit 1
ENTRYPOINT ["dotnet", "BattleCatCalc.Api.dll"]
