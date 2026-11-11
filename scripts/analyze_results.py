import csv
from collections import defaultdict
from pathlib import Path
from statistics import mean
import matplotlib.pyplot as plt

repo_dir = Path(__file__).resolve().parent.parent
results_dir = repo_dir / "results"

groups = ["out_of_order", "clean_naive", "clean_recovery"]

for group in groups:
    runs_by_strategy = defaultdict(list)
    csv_files = sorted((results_dir / group).glob("*.csv"))

    if not csv_files:
        raise SystemExit(f"No CSV files found for {group}")

    for csv_file in csv_files:
        with csv_file.open(newline="", encoding="utf-8-sig") as file:
            for row in csv.DictReader(file):
                runs_by_strategy[row["strategy"]].append(row)

    print(f"\nExperiment: {group}")

    for strategy, runs in runs_by_strategy.items():
        stale_percent = mean(
            float(row["stale_read_percent"]) for row in runs
        )
        throughput = mean(
            float(row["operations_per_second"]) for row in runs
        )
        read_p95 = mean(
            float(row["read_p95_ms"]) for row in runs
        )
        write_p95 = mean(
            float(row["write_p95_ms"]) for row in runs
        )

        print(f"\n  {strategy}")
        print(f"    Runs: {len(runs)}")
        print(f"    Mean stale-read percentage: {stale_percent:.2f}%")
        print(f"    Mean throughput: {throughput:.1f} ops/s")
        print(f"    Mean per-run read p95: {read_p95:.3f} ms")
        print(f"    Mean per-run write p95: {write_p95:.3f} ms")

output_dir = results_dir / "analysis"
output_dir.mkdir(exist_ok=True)

strategy_names = {
    "Write-Through (write DB then cache, same path)": "Write-through",
    "Versioned Write-Through (monotonic, version-guarded)": "Version-aware",
}

def load_stale_rates(group):
    rates = defaultdict(list)

    for csv_file in sorted((results_dir / group).glob("*.csv")):
        with csv_file.open(newline="", encoding="utf-8-sig") as file:
            for row in csv.DictReader(file):
                strategy = row["strategy"]
                if strategy in strategy_names:
                    rates[strategy].append(float(row["stale_read_percent"]))

    return rates

# Graph 1: Out-of-order operations
rates = load_stale_rates("out_of_order")
fig, ax = plt.subplots(figsize=(8, 5))

for position, (strategy, label) in enumerate(strategy_names.items()):
    values = rates[strategy]
    average = mean(values)

    ax.bar(position, average, width=0.55,
           color=["#64748b", "#2563eb"][position])
    ax.scatter([position] * len(values), values,
               color="black", s=30, zorder=3)
    ax.text(position, max(values) + 2, f"{average:.2f}%",
            ha="center", fontweight="bold")

ax.set_xticks([0, 1], list(strategy_names.values()))
ax.set_ylim(0, 60)
ax.set_ylabel("Stale reads among stable validations (%)")
ax.set_title("Stale reads under out-of-order operations")
ax.spines[["top", "right"]].set_visible(False)

fig.text(
    0.5, 0.02,
    "50 workers • 80/20 reads/writes • 1 product • 20 seconds per run\n"
    "30% reorder-delay rate, 50 ms delay • Mean of 3 runs; dots show each run",
    ha="center", fontsize=9,
)
fig.tight_layout(rect=(0, 0.10, 1, 1))
fig.savefig(output_dir / "out_of_order_stale_reads.png", dpi=300)
plt.close(fig)

# Graph 2: Recovery disabled versus enabled
fig, ax = plt.subplots(figsize=(8, 5))
width = 0.32

for group, offset, label, color in [
    ("clean_naive", -width / 2, "Recovery disabled", "#64748b"),
    ("clean_recovery", width / 2, "Recovery enabled", "#2563eb"),
]:
    rates = load_stale_rates(group)

    for position, strategy in enumerate(strategy_names):
        values = rates[strategy]
        average = mean(values)
        x = position + offset

        ax.bar(x, average, width=width, color=color,
               label=label if position == 0 else None)
        ax.scatter([x] * len(values), values,
                   color="black", s=25, zorder=3)
        ax.text(x, max(values) + 0.5, f"{average:.2f}%",
                ha="center", fontsize=10)

ax.set_xticks([0, 1], list(strategy_names.values()))
ax.set_ylim(0, 18)
ax.set_ylabel("Stale reads among stable validations (%)")
ax.set_title("Recovery reduces stale reads during cache-update failures")
ax.legend(frameon=False)
ax.spines[["top", "right"]].set_visible(False)

fig.text(
    0.5, 0.02,
    "50 workers • 80/20 reads/writes • 1 product • 20 seconds per run\n"
    "10% cache-update failure rate • No artificial delays\n"
    "Mean of 3 runs; dots show each run",
    ha="center", fontsize=9,
)
fig.tight_layout(rect=(0, 0.14, 1, 1))
fig.savefig(output_dir / "recovery_stale_reads.png", dpi=300)
plt.close(fig)

print(f"\nGraphs saved to: {output_dir}")