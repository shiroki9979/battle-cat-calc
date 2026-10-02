# Container workflows

Created: 2026-10-01
Last updated: 2026-10-02

## Requirements and layout

Run the following Compose commands from the repository root. Docker Engine or
Docker Desktop, Buildx, and Docker Compose 2.24 or later are required. Development
in VS Code also requires the Dev Containers extension. The host does not need a
.NET SDK, Node.js, npm, or browser installation.

## Make shortcuts

GNU Make and a POSIX shell provide shortcuts from the repository root. `make`
prints the available targets. Make exports the host UID/GID automatically;
explicit `DEV_UID` and `DEV_GID` values can override them.

```bash
make build
make test
make test-dotnet TEST_ARGS='--filter FullyQualifiedName~BCParserSnapshotTests'
make test-e2e E2E_ARGS='--project chromium'
make dev
```

`restore`, `build`, `test-dotnet`, `publish`, `shell`, and `dev` first build the SDK image.
`make build` includes a clean npm install, TypeScript checking, and a Vite build.
`make test` runs the .NET suite and the browser suite; use `make test-dotnet` or
`make test-e2e` for a single suite. Browser arguments use `E2E_ARGS`.
`make publish` writes the API and client to `/artifacts/publish/api`.
`make image` builds the production image; `make up` also starts it and waits for
health. Use `make health`, `make logs`, and `make down` to inspect or stop it.
Before switching from development to production, run `make down` to release port
5096. The `down` target preserves all volumes and host test reports. `COMPOSE` can
override the Docker Compose command when a separate Docker configuration is used.

## Image inputs and ownership

The SDK and ASP.NET images use the official `10.0-noble` tags and Linux amd64.
Node.js 24 and npm come from the official `node:24-bookworm-slim` image. A separate
E2E stage installs the browser versions selected by the locked Playwright package
and their Linux dependencies. Development uses this stage; production contains
the ASP.NET runtime and built client assets.
The tags receive patch updates; rebuild with `--pull` to refresh the base images.
The repository root is the build context because .NET and Python share the HTML
fixtures in `tests/fixtures`. The Docker ignore rules limit the image inputs to
the .NET project and those fixtures.

SDK commands run as the non-root `app` user, with UID/GID 1000 by default. On Linux,
set `DEV_UID` and `DEV_GID` to the host account's IDs before building:

```bash
export DEV_UID="$(id -u)"
export DEV_GID="$(id -g)"
docker compose build sdk
```

For VS Code launched outside that shell, persist these two values in the
repository's ignored `.env` file. Dev Containers uses the image's account without
remapping it a second time. Rebuild the development image when these IDs change,
and recreate its NuGet/artifact volumes to update ownership.

## Build, test, and publish

From a fresh checkout:

```bash
make build
make test
make publish
```

The test service runs all four test projects and propagates failures through its
exit code. The existing HTTP mocks and API stubs keep tests independent of live
websites, databases, and browsers. A filtered run uses the same entry point:

```bash
docker compose run --rm test --filter FullyQualifiedName~BCParserSnapshotTests
```

Build the client and run the browser tests without host Node.js:

```bash
make client-build
make test-e2e
```

Playwright starts Vite and runs Chromium, Firefox, and WebKit against mocked API
responses. HTML reports, failure screenshots, and traces are written under
`test-results/playwright-report` and `test-results/playwright-results` on the host.
The browser test service returns a nonzero exit code on failure.

TRX reports and per-project Cobertura XML files are written under the host's
`test-results` directory. The tracked placeholder ensures that the bind mount
exists and belongs to the checkout's owner. Existing reports remain available
after a failed run. No coverage percentage gate is imposed.

NuGet packages and all `bin`, `obj`, and publish outputs live in named Docker
volumes. `Directory.Build.props` selects the centralized .NET artifacts layout
only when the container provides `CONTAINER_ARTIFACTS_PATH`.
Frontend `node_modules` and `dist` also use named volumes. SDK, development, and
browser containers share these volumes; stop development before building or
testing. Each client build and browser run starts with `npm ci`.

## Development and debugging

Open the repository in VS Code and run **Dev Containers: Reopen in Container**.
The container restores the solution on creation. The C# extension, language
service, terminals, tasks, debugger, Node.js, and browsers run inside the
development container. A separate `client` service starts Vite automatically.

Start **Tasks: Run Task → watch API**, or run this in its terminal:

```bash
cd /workspace/dotnet
dotnet watch --non-interactive --project src/BattleCatCalc.Api run --no-launch-profile
```

The client is available at `http://localhost:5173`; its `/api` proxy reaches
`http://sdk:8080` on the Compose network. `API_PROXY_TARGET` overrides the proxy
target when another API environment is used. The API is available at
`http://localhost:5096`. Saving source changes applies Hot
Reload when supported, otherwise the application restarts. Polling file watching
is enabled by the official SDK image for bind-mounted files. The launch profile
is bypassed so Kestrel listens on the container's externally reachable port 8080.

For debugging, stop watch with Ctrl+C or **Tasks: Terminate Task**, then select
**Debug API (Dev Container)** and press F5. Set a breakpoint in a controller and
call its endpoint. Stop debugging with Shift+F5 before starting watch again.
Watch and the debugger share port 8080 and run one at a time. Stop the production
API before opening the development container because both use host port 5096.

The same environment can run without VS Code:

```bash
docker compose stop api
docker compose -f compose.yaml -f .devcontainer/compose.yaml up -d --build sdk client
docker compose -f compose.yaml -f .devcontainer/compose.yaml exec sdk \
  dotnet watch --non-interactive --project src/BattleCatCalc.Api run --no-launch-profile
```

Stop watch before running other builds or tests against the shared artifact
volume. Source files stay synchronized with the host; development also allows
editing the shared fixtures. Test-only containers mount the fixtures read-only.

## Production deployment

```bash
docker compose build api
docker compose up -d --wait --wait-timeout 60 api
docker compose exec -T api curl --fail --silent --show-error \
  http://localhost:8080/api/v1/health
docker compose logs --follow api
```

The final image contains the published API, built React client in `/app/wwwroot`,
ASP.NET runtime, and `curl` for HTTP
health checks. It runs as the runtime image's non-root `app` account (UID 1654),
uses the Production environment, and exposes host HTTP 5096 through container
8080. An external reverse proxy terminates HTTPS. The API needs outbound HTTPS
access to `bc.godfat.org` for live track fetching; builds need NuGet, npm, and
Playwright download access. `make health` checks the API and deployed client HTML.

Console and warning-level file logging retain the existing NLog configuration,
including daily rotation and the seven-archive limit. The `logs` volume is mounted
at `/app/logs` and survives container recreation. Read the files with:

```bash
docker compose exec -T api sh -c 'cat /app/logs/battlecatcalc-*.log'
docker compose stop api
```

`docker compose down` removes containers and networks while retaining volumes.
To clear only SDK caches for the default project, first stop the development
container, then run:

```bash
docker compose --profile tools down
docker volume rm battle-cat-calc_nuget battle-cat-calc_artifacts \
  battle-cat-calc_client_modules battle-cat-calc_client_dist
```

The logs volume remains intact. `down --volumes` also deletes the stored logs;
reserve it for disposable environments such as CI. Host test reports can be
removed separately while keeping `test-results/.gitkeep`.

## Continuous integration

Pull requests and pushes to `main` or `dev` build the client and run both Compose test services. GitHub
Actions matches the SDK UID/GID to the runner's account, uploads TRX and Cobertura
reports as the `dotnet-test-results` artifact even after test failures, and builds
the production image only after successful tests. The same artifact also includes
browser reports and failure diagnostics.

CI starts the runtime image, waits at most 60 seconds for its HTTP health check,
and checks the health endpoint and deployed client HTML. Build, test, startup, and health failures fail the
job and print container diagnostics. Cleanup runs even after failures and removes
the job's isolated containers and volumes. Image registry publication is handled
separately.

## References

- [.NET artifacts output layout](https://learn.microsoft.com/en-us/dotnet/core/sdk/artifacts-output)
- [dotnet watch](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-watch)
- [Dev Container non-root users](https://code.visualstudio.com/remote/advancedcontainers/add-nonroot-user)
