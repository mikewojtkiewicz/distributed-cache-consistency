#!/usr/bin/env bash
#
# Reproducible benchmark matrix for the CacheConsistency project.
#
# Runs the full experiment set against the Docker Compose Postgres + Redis
# stack and writes timestamped CSV + JSON results into results/<group>/.
# Each invocation exercises all four caching strategies; the output schema is
# unchanged, so the analysis scripts can ingest the files directly. Groups are
# separated into subfolders because recovery mode and key-count are not CSV
# columns.
#
# Usage:
#   ./run-experiments.sh                 # defaults: REPS=3, DUR=20 (~1 hour)
#   REPS=1 DUR=15 ./run-experiments.sh   # quick single pass (~15 min)
#
set -euo pipefail

REPS="${REPS:-3}"
DUR="${DUR:-20}"

DELAYS="--min-delay-ms 1 --max-delay-ms 10"
FAIL="--cache-update-failure-rate 0.1 --invalidation-failure-rate 0.1"
OOO="--out-of-order-rate 0.3 --out-of-order-delay-ms 50"

echo "Matrix: REPS=$REPS, DUR=${DUR}s per strategy. Bringing up postgres + redis..."
docker compose up -d postgres redis

# run <group-folder> <extra app args...>
run() {
  local out="$1"; shift
  docker compose run --rm app \
    --workers "$WORKERS" --ratio "$RATIO" --duration-seconds "$DUR" \
    --output-dir "results/$out" "$@"
}

for rep in $(seq 1 "$REPS"); do
  echo "===== REP $rep/$REPS ====="

  # 1) Baseline sweep (no faults): concurrency x read/write ratio.
  for WORKERS in 10 50 100; do
    for RATIO in 90/10 80/20 50/50; do
      run baseline
    done
  done

  # Fixed operating point for the fault / consistency groups.
  WORKERS=50
  RATIO=80/20

  # 2) Failures + delays, recovery off vs on.
  run faults_naive    $DELAYS $FAIL
  run faults_recovery $DELAYS $FAIL --recovery

  # 3) Failures only (no delays) — isolates the recovery effect cleanly.
  run clean_naive    $FAIL
  run clean_recovery $FAIL --recovery

  # 4) Out-of-order cache writes — isolates version-aware vs write-through.
  run out_of_order $OOO

  # 5) Multiple products (20 rows, 3 hot) — baseline and under out-of-order.
  run multikey        --key-count 20 --hot-keys 3
  run multikey_stress --key-count 20 --hot-keys 3 $OOO
done

echo "Done. Results in results/<group>/*.csv (+ .json). Stopping stack..."
docker compose down
