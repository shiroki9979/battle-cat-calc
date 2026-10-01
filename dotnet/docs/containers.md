# Container workflows

Created: 2026-10-01
Last updated: 2026-10-01

## Requirements and layout

Run the following Compose commands from the repository root. Docker Engine or
Docker Desktop, Buildx, and Docker Compose 2.24 or later are required. Development
in VS Code also requires the Dev Containers extension. The host does not need a
.NET SDK.

The SDK and ASP.NET images use the official `10.0-noble` tags and Linux amd64.
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
docker compose build sdk
docker compose run --rm sdk dotnet restore
docker compose run --rm sdk dotnet build --no-restore
docker compose run --rm test
docker compose run --rm sdk dotnet publish src/BattleCatCalc.Api --configuration Release
```

The test service runs all four test projects and propagates failures through its
exit code. The existing HTTP mocks and API stubs keep tests independent of live
websites, databases, and browsers. A filtered run uses the same entry point:

```bash
docker compose run --rm test --filter FullyQualifiedName~BCParserSnapshotTests
```

TRX reports and per-project Cobertura XML files are written under the host's
`test-results` directory. The tracked placeholder ensures that the bind mount
exists and belongs to the checkout's owner. Existing reports remain available
after a failed run. No coverage percentage gate is imposed.

NuGet packages and all `bin`, `obj`, and publish outputs live in named Docker
volumes. `Directory.Build.props` selects the centralized .NET artifacts layout
only when the container provides `CONTAINER_ARTIFACTS_PATH`.

## Development and debugging

Open the repository in VS Code and run **Dev Containers: Reopen in Container**.
The container restores the solution on creation. The C# extension, language
service, terminals, tasks, and debugger run inside the SDK container.

Start **Tasks: Run Task → watch API**, or run this in its terminal:

```bash
cd /workspace/dotnet
dotnet watch --non-interactive --project src/BattleCatCalc.Api run --no-launch-profile
```

The API is available at `http://localhost:5096`. Saving source changes applies Hot
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
docker compose -f compose.yaml -f .devcontainer/compose.yaml up -d sdk
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

The final image contains the published API, ASP.NET runtime, and `curl` for HTTP
health checks. It runs as the runtime image's non-root `app` account (UID 1654),
uses the Production environment, and exposes host HTTP 5096 through container
8080. An external reverse proxy terminates HTTPS. The API needs outbound HTTPS
access to `bc.godfat.org` for live track fetching; restore needs NuGet access.

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
docker volume rm battle-cat-calc_nuget battle-cat-calc_artifacts
```

The logs volume remains intact. `down --volumes` also deletes the stored logs;
reserve it for disposable environments such as CI. Host test reports can be
removed separately while keeping `test-results/.gitkeep`.

## Continuous integration

Pull requests and pushes to `main` run the same Compose test service. GitHub
Actions matches the SDK UID/GID to the runner's account, uploads TRX and Cobertura
reports as the `dotnet-test-results` artifact even after test failures, and builds
the production image only after successful tests.

CI starts the runtime image, waits at most 60 seconds for its HTTP health check,
and calls the health endpoint. Build, test, startup, and health failures fail the
job and print container diagnostics. Cleanup runs even after failures and removes
the job's isolated containers and volumes. Image registry publication is handled
separately.

## References

- [.NET artifacts output layout](https://learn.microsoft.com/en-us/dotnet/core/sdk/artifacts-output)
- [dotnet watch](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-watch)
- [Dev Container non-root users](https://code.visualstudio.com/remote/advancedcontainers/add-nonroot-user)
