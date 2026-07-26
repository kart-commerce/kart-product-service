# kart-product-service

Catalog system-of-record: product/variant creation and edit (Admin/Partner-API-called write
surface) plus the public SKU-keyed catalog read. The platform's highest-fan-out catalog publisher
(`ProductCreated`/`ProductPriceChanged`/`ProductUpdated`/`ProductDiscontinued`).

Design docs: `kart-platform/docs/services/kart-product-service/`. Tickets: PRD-1 through PRD-6.

## Architecture

CQRS with a genuine write/read database split:

- **Write side**: PostgreSQL, via EF Core - two aggregates (`ProductGroup`, `Variant`, per
  `ddd-model.md`'s transaction-boundary split) plus the Transactional Outbox
  (`product_outbox_events`).
- **Read side**: MongoDB, `product_read_model` - one denormalized document per SKU, **sharded on
  `category.id`**. Never written directly by a request handler; kept in sync purely through the
  message bus (see below), so it stays independently rebuildable from PostgreSQL + the event log.
- **Sync mechanism**: write -> Outbox row (same transaction) -> `OutboxRelayHostedService`
  publishes to `product.exchange` -> this service's own `product.catalog-projection.queue`
  (self-consumption of the four events it just published) -> field-scoped Mongo `$set`. The same
  manifest-driven topology/retry-ladder/DLQ machinery used for any other consumer, not a bespoke
  in-process sync path.
- **Message bus topology**: `contracts/message-bus-manifest.json` is the single source of truth,
  declared idempotently at startup (`RabbitMqTopologyProvisioner`) - nothing is hardcoded in C#.
  See `contracts/README.md`.

## Layout

Clean Architecture + Vertical Slice (`docs/standards/folder-structure.md` in
[agent-reusables](https://github.com/kakon-mehedi/agent-reusables)):

```
src/
├── Api/              # controllers, JWT auth, global exception handling, observability wiring
├── Application/       # Features/<UseCaseName>/ vertical slices (MediatR)
├── Domain/             # ProductGroup/Variant aggregates, value objects, domain events
└── Infrastructure/    # EF Core (Postgres), MongoDB read model, RabbitMQ messaging, outbox
tests/
├── UnitTests/          # colocated by feature, mirrors Application/Features
├── IntegrationTests/   # Testcontainers: Postgres + MongoDB + RabbitMQ
└── ContractTests/      # validates live responses against contracts/api-contract.yaml
contracts/              # synced copies of the approved api-contract.yaml / message-bus-manifest.json
```

Reuses `kart-shared`'s `Kart.Shared.Domain` (Result/Error/AggregateRoot base types),
`Kart.Shared.ErrorHandling` (the one global exception handler + `ProblemDetails` envelope, every
error response), and `Kart.Shared.Observability` (Serilog + OpenTelemetry + Prometheus, one DI
call) via `ProjectReference` - no published NuGet feed exists yet (`kart-shared`'s own README
documents this as the interim consumption path).

## Running locally

Requires the .NET 8 SDK and Docker.

```
dotnet build
dotnet test
```

### Full stack via docker-compose

```
docker compose up -d --build
./scripts/init-mongo-cluster.sh   # one-time: initializes the sharded Mongo cluster
```

This starts PostgreSQL, RabbitMQ, a real (minimal) sharded MongoDB cluster (config-server replset
+ two shard replsets + a `mongos` router), and the service itself on `http://localhost:8080`.

> The Docker build context is the **parent** directory (`kart-commerce/`), not this repo alone -
> see the comment at the top of `Dockerfile`. This is a known, temporary consequence of
> `kart-shared` not yet publishing to a real NuGet feed.

### Running against locally-installed dependencies instead

```
dotnet ef database update --project src/Infrastructure --startup-project src/Api
dotnet run --project src/Api
```

Point `ConnectionStrings__ProductDatabase`, `Mongo__ConnectionString`/`Mongo__Database`, and
`RabbitMq__HostName` at your own Postgres/Mongo/RabbitMQ instances via environment variables or
`appsettings.Development.json` if they differ from the defaults in `src/Api/appsettings.json`.

### Auth

Write endpoints require a client-credentials JWT with an `admin` or `partner` scope
(`api-contract.yaml`'s `clientCredentials` security scheme), validated against the symmetric key
in `Jwt:SigningKey` for local/dev use (production would point at kart-identity-service's JWKS
endpoint instead). `GET /v1/products/{sku}` is unconditionally public.
