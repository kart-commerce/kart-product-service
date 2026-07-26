# Contracts

`api-contract.yaml` is a synced copy of the approved contract owned by
`kart-platform/docs/services/kart-product-service/api-contract.yaml` (the
source of truth). It is vendored here so `tests/ContractTests` can validate
this service's actual HTTP responses against it in this repo's own CI,
without a cross-repo checkout. Update it only by re-copying the upstream
file after a new contract revision is approved there — never edit it
directly in this repo.

`message-bus-manifest.json` is likewise a synced copy of
`kart-platform/docs/services/kart-product-service/message-bus-manifest.json`
— this service's own RabbitMQ topology (`product.exchange`/`product.dlx`,
owned by this service alone; no shared platform-wide exchange, per
`kart-conventions.md` §"RabbitMQ" and `kart-requirements.md` §8/§9).
Declared idempotently at startup by `RabbitMqTopologyStartupHostedService`
(`src/Infrastructure/Messaging`), which walks this file via
`RabbitMqTopologyProvisioner` — nothing in this topology is hardcoded in
C#. Two queues are declared from it:

- `product.review-events.queue` — consumes `ReviewSubmitted`/`ReviewUpdated`
  from the externally-owned `review.exchange` (ticket PRD-6), projecting
  only the `ratingSummary` field of `product_read_model`.
- `product.catalog-projection.queue` — this service's own **self-consumption**
  of the four events it publishes onto `product.exchange`
  (`ProductCreated`/`ProductPriceChanged`/`ProductUpdated`/`ProductDiscontinued`),
  which is how the PostgreSQL write side stays synced to the denormalized
  MongoDB read side: write → outbox → publish → this queue → field-scoped
  Mongo upsert. This reuses the exact same manifest-driven topology/retry-
  ladder/DLQ machinery as any other consumer instead of a bespoke in-process
  sync path.

Update `message-bus-manifest.json` only by re-copying the upstream file
after a manifest revision is approved there.
