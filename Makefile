COMPOSE ?= docker compose
DEV_COMPOSE = $(COMPOSE) -f compose.yaml -f .devcontainer/compose.yaml
export DEV_UID ?= $(shell id -u)
export DEV_GID ?= $(shell id -g)

.DEFAULT_GOAL := help
# Builds share the artifact volume.
.NOTPARALLEL:
.PHONY: help sdk restore build client-build test test-dotnet test-e2e publish shell dev image up health logs down

help:
	@printf '%s\n' \
	  'make sdk       Build the SDK image' \
	  'make restore   Restore NuGet packages' \
	  'make build     Build the .NET solution' \
	  'make test      Run .NET and browser tests in containers' \
	  'make test-dotnet Run .NET tests with TRX and coverage reports' \
	  '               Append test options with TEST_ARGS="--filter ..."' \
	  'make test-e2e  Run Playwright on Chromium, Firefox, and WebKit' \
	  '               Append browser options with E2E_ARGS="--project chromium"' \
	  'make client-build Install, typecheck, and build the React client' \
	  'make publish   Publish the API in the SDK container' \
	  'make shell     Open a shell in the SDK container' \
	  'make dev       Start the development API with dotnet watch' \
	  'make image     Build the production image' \
	  'make up        Build and start the production API' \
	  'make health    Check the production health endpoint' \
	  'make logs      Follow production console logs' \
	  'make down      Stop containers; retain caches and logs'

sdk:
	$(COMPOSE) build sdk

restore: sdk
	$(COMPOSE) run --rm sdk dotnet restore

build: sdk client-build
	$(COMPOSE) run --rm sdk dotnet build

client-build: sdk
	$(COMPOSE) run --rm sdk sh -c 'cd client && npm ci && npm run build'

test: test-dotnet test-e2e

test-dotnet: sdk
	$(COMPOSE) run --rm test $(TEST_ARGS)

test-e2e:
	$(COMPOSE) build e2e
	$(COMPOSE) run --rm e2e $(E2E_ARGS)

publish: sdk client-build
	$(COMPOSE) run --rm sdk sh -c 'dotnet publish src/BattleCatCalc.Api --configuration Release --output /artifacts/publish/api && mkdir -p /artifacts/publish/api/wwwroot && cp -R client/dist/. /artifacts/publish/api/wwwroot/'

shell: sdk
	$(COMPOSE) run --rm sdk bash

dev: sdk
	$(COMPOSE) stop api
	$(DEV_COMPOSE) up -d --build sdk client
	$(DEV_COMPOSE) exec sdk dotnet watch --non-interactive --project src/BattleCatCalc.Api run --no-launch-profile

image:
	$(COMPOSE) build api

up: image
	$(COMPOSE) up -d --wait --wait-timeout 60 api

health:
	$(COMPOSE) exec -T api curl --fail --silent --show-error --max-time 5 http://localhost:8080/api/v1/health
	$(COMPOSE) exec -T api curl --fail --silent --show-error --max-time 5 --output /dev/null http://localhost:8080/index.html

logs:
	$(COMPOSE) logs --follow api

down:
	$(COMPOSE) --profile tools down
