#!/usr/bin/env bash
# Initializes the sharded MongoDB cluster brought up by docker-compose.yml: one config-server
# replset, two shard replsets, one mongos router - then shards product_read_model on category.id
# (database-design.md). Run this once, after `docker compose up -d` and before the API starts
# consuming Mongo. Safe to re-run (rs.initiate/sh.addShard/sh.shardCollection are idempotent -
# Mongo returns an "already initialized"/"already sharded" error that this script tolerates).
set -euo pipefail

wait_for_mongo() {
  local container=$1
  local port=$2
  echo "Waiting for $container:$port ..."
  for _ in $(seq 1 30); do
    if docker exec "$container" mongosh --quiet --port "$port" --eval "db.runCommand('ping')" >/dev/null 2>&1; then
      echo "$container:$port is up"
      return 0
    fi
    sleep 2
  done
  echo "Timed out waiting for $container:$port" >&2
  exit 1
}

wait_for_mongo kart-product-mongo-configsvr 27019
wait_for_mongo kart-product-mongo-shard1 27018
wait_for_mongo kart-product-mongo-shard2 27018

echo "Initiating config server replica set..."
docker exec kart-product-mongo-configsvr mongosh --quiet --port 27019 --eval '
  try {
    rs.initiate({ _id: "productCfgRS", configsvr: true, members: [{ _id: 0, host: "kart-product-mongo-configsvr:27019" }] });
  } catch (e) {
    if (!String(e).includes("already initialized")) { throw e; }
  }
'

echo "Initiating shard 1 replica set..."
docker exec kart-product-mongo-shard1 mongosh --quiet --port 27018 --eval '
  try {
    rs.initiate({ _id: "productShard1RS", members: [{ _id: 0, host: "kart-product-mongo-shard1:27018" }] });
  } catch (e) {
    if (!String(e).includes("already initialized")) { throw e; }
  }
'

echo "Initiating shard 2 replica set..."
docker exec kart-product-mongo-shard2 mongosh --quiet --port 27018 --eval '
  try {
    rs.initiate({ _id: "productShard2RS", members: [{ _id: 0, host: "kart-product-mongo-shard2:27018" }] });
  } catch (e) {
    if (!String(e).includes("already initialized")) { throw e; }
  }
'

echo "Waiting for replica sets to elect a primary..."
sleep 10

echo "Waiting for mongos router..."
wait_for_mongo kart-product-mongo-router 27017

echo "Adding shards to the cluster..."
docker exec kart-product-mongo-router mongosh --quiet --port 27017 --eval '
  try { sh.addShard("productShard1RS/kart-product-mongo-shard1:27018"); } catch (e) { if (!String(e).includes("duplicate")) { print(e); } }
  try { sh.addShard("productShard2RS/kart-product-mongo-shard2:27018"); } catch (e) { if (!String(e).includes("duplicate")) { print(e); } }
'

echo "Enabling sharding on the kart database and sharding product_read_model on category.id..."
docker exec kart-product-mongo-router mongosh --quiet --port 27017 --eval '
  sh.enableSharding("kart");
  db.getSiblingDB("kart").product_read_model.createIndex({ "category.id": 1 });
  sh.shardCollection("kart.product_read_model", { "category.id": 1 });
'

echo "Sharding status:"
docker exec kart-product-mongo-router mongosh --quiet --port 27017 --eval 'sh.status()'

echo "Mongo cluster initialized."
