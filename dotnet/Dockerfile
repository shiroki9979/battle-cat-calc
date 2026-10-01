FROM node:24-bookworm-slim AS node

FROM node AS client-build
WORKDIR /client
COPY dotnet/client/package*.json ./
RUN npm ci
COPY dotnet/client/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS sdk-deps

COPY --from=node /usr/local/bin/node /usr/local/bin/node
COPY --from=node /usr/local/lib/node_modules/npm /usr/local/lib/node_modules/npm
RUN ln -s ../lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm \
    && ln -s ../lib/node_modules/npm/bin/npx-cli.js /usr/local/bin/npx

ARG DEV_UID=1000
ARG DEV_GID=1000
RUN if id ubuntu >/dev/null 2>&1; then userdel --remove ubuntu; fi \
    && groupmod --gid "$DEV_GID" app \
    && usermod --uid "$DEV_UID" --gid "$DEV_GID" app \
    && mkdir -p /workspace/dotnet/client/dist /workspace/tests/fixtures /artifacts /test-results /home/app/.nuget/packages \
    && chown -R app:app /workspace /artifacts /test-results /home/app

ENV CONTAINER_ARTIFACTS_PATH=/artifacts \
    NUGET_PACKAGES=/home/app/.nuget/packages \
    DOTNET_CLI_HOME=/home/app \
    DOTNET_NOLOGO=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

WORKDIR /workspace/dotnet
COPY --chown=app:app dotnet/client/package*.json /workspace/dotnet/client/
USER app
RUN cd client && npm ci

FROM sdk-deps AS browser-deps
USER root
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
RUN cd client && npx playwright install --with-deps chromium firefox webkit
USER app

FROM sdk-deps AS sdk
COPY --chown=app:app dotnet/ /workspace/dotnet/
COPY --chown=app:app tests/fixtures/ /workspace/tests/fixtures/
CMD ["sleep", "infinity"]

FROM browser-deps AS e2e
COPY --chown=app:app dotnet/ /workspace/dotnet/
COPY --chown=app:app tests/fixtures/ /workspace/tests/fixtures/
WORKDIR /workspace/dotnet/client
ENTRYPOINT ["sh", "-c", "npm ci && exec npm run test:e2e -- \"$@\"", "--"]
CMD []

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
COPY --from=client-build --chown=app:app /client/dist/ /app/wwwroot/
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
USER app
EXPOSE 8080
HEALTHCHECK --interval=5s --timeout=3s --start-period=10s --retries=10 \
    CMD curl --fail --silent --show-error http://localhost:8080/api/v1/health || exit 1
ENTRYPOINT ["dotnet", "BattleCatCalc.Api.dll"]
