# CacheConsistency — Benchmark Dataset Summary

Generated: 2026-11-10 · ephemeral postgres:16 + redis:7 · main @ PR#3 merged
3 reps per config · 20s per strategy per run · all 4 strategies per run.
Raw per-run files live in each group folder as timestamped .csv/.json (schema unchanged).

Groups: baseline (no faults); faults_* (failures + 1-10ms delays); clean_* (failures only, no delays);
out_of_order; multikey / multikey_stress (20 keys, 3 hot).

## Recovery effect (clean_* : failures only, no delays)
| strategy | naive stale% | recovery stale% | naive read-fail | recovery read-fail |
|---|---:|---:|---:|---:|
| CacheAside | 12.65 | 12.45 | 631 | 0 |
| TTL | 99.98 | 99.90 | 2 | 2 |
| WriteThrough | 12.04 | 2.73 | 1 | 0 |
| Versioned | 11.86 | 2.53 | 2 | 0 |

## faults_naive  (mean over reps)
| strategy | stale% | ops/s | read_p95ms |
|---|---:|---:|---:|
| CacheAside | 68.31 | 1630 | 18.711 |
| TTL | 99.92 | 1700 | 10.365 |
| WriteThrough | 71.32 | 1683 | 10.380 |
| Versioned | 61.51 | 1640 | 10.417 |

## faults_recovery  (mean over reps)
| strategy | stale% | ops/s | read_p95ms |
|---|---:|---:|---:|
| CacheAside | 64.71 | 1592 | 18.250 |
| TTL | 99.82 | 1721 | 10.390 |
| WriteThrough | 72.06 | 1664 | 10.794 |
| Versioned | 56.07 | 1665 | 10.902 |

## clean_naive  (mean over reps)
| strategy | stale% | ops/s | read_p95ms |
|---|---:|---:|---:|
| CacheAside | 12.65 | 1548 | 2.725 |
| TTL | 99.98 | 1616 | 0.601 |
| WriteThrough | 12.04 | 1588 | 0.542 |
| Versioned | 11.86 | 1589 | 0.545 |

## clean_recovery  (mean over reps)
| strategy | stale% | ops/s | read_p95ms |
|---|---:|---:|---:|
| CacheAside | 12.45 | 1536 | 2.416 |
| TTL | 99.90 | 1640 | 0.619 |
| WriteThrough | 2.73 | 1578 | 0.617 |
| Versioned | 2.53 | 1588 | 0.624 |

## out_of_order  (mean over reps)
| strategy | stale% | ops/s | read_p95ms |
|---|---:|---:|---:|
| CacheAside | 41.18 | 1567 | 51.108 |
| TTL | 99.90 | 1649 | 0.750 |
| WriteThrough | 39.13 | 1633 | 0.426 |
| Versioned | 22.51 | 1620 | 0.506 |

## multikey  (mean over reps)
| strategy | stale% | ops/s | read_p95ms |
|---|---:|---:|---:|
| CacheAside | 1.51 | 3119 | 11.693 |
| TTL | 99.79 | 3516 | 0.882 |
| WriteThrough | 0.56 | 3527 | 0.668 |
| Versioned | 0.57 | 3583 | 0.881 |

## multikey_stress  (mean over reps)
| strategy | stale% | ops/s | read_p95ms |
|---|---:|---:|---:|
| CacheAside | 32.44 | 3022 | 52.033 |
| TTL | 99.77 | 3489 | 0.825 |
| WriteThrough | 39.02 | 3349 | 0.892 |
| Versioned | 25.81 | 3325 | 0.960 |

## baseline (no faults) — by load point
| workers | ratio | strategy | stale% | ops/s |
|---:|---|---|---:|---:|
| 10 | 50/50 | CacheAside | 1.98 | 854 |
| 10 | 50/50 | TTL | 99.97 | 885 |
| 10 | 50/50 | WriteThrough | 0.74 | 865 |
| 10 | 50/50 | Versioned | 0.84 | 852 |
| 10 | 80/20 | CacheAside | 4.82 | 2425 |
| 10 | 80/20 | TTL | 99.96 | 2476 |
| 10 | 80/20 | WriteThrough | 2.00 | 2308 |
| 10 | 80/20 | Versioned | 2.05 | 2305 |
| 10 | 90/10 | CacheAside | 4.03 | 3828 |
| 10 | 90/10 | TTL | 99.96 | 4440 |
| 10 | 90/10 | WriteThrough | 1.71 | 4367 |
| 10 | 90/10 | Versioned | 1.74 | 4370 |
| 50 | 50/50 | CacheAside | 1.27 | 637 |
| 50 | 50/50 | TTL | 99.92 | 688 |
| 50 | 50/50 | WriteThrough | 0.87 | 673 |
| 50 | 50/50 | Versioned | 0.96 | 670 |
| 50 | 80/20 | CacheAside | 2.07 | 1520 |
| 50 | 80/20 | TTL | 99.94 | 1662 |
| 50 | 80/20 | WriteThrough | 1.67 | 1599 |
| 50 | 80/20 | Versioned | 1.57 | 1603 |
| 50 | 90/10 | CacheAside | 2.82 | 2730 |
| 50 | 90/10 | TTL | 99.72 | 3042 |
| 50 | 90/10 | WriteThrough | 1.89 | 2947 |
| 50 | 90/10 | Versioned | 1.85 | 2897 |
| 100 | 50/50 | CacheAside | 1.40 | 602 |
| 100 | 50/50 | TTL | 99.79 | 650 |
| 100 | 50/50 | WriteThrough | 1.01 | 641 |
| 100 | 50/50 | Versioned | 0.96 | 639 |
| 100 | 80/20 | CacheAside | 2.35 | 1384 |
| 100 | 80/20 | TTL | 99.94 | 1532 |
| 100 | 80/20 | WriteThrough | 1.79 | 1483 |
| 100 | 80/20 | Versioned | 1.57 | 1486 |
| 100 | 90/10 | CacheAside | 2.73 | 2599 |
| 100 | 90/10 | TTL | 99.91 | 2836 |
| 100 | 90/10 | WriteThrough | 1.74 | 2692 |
| 100 | 90/10 | Versioned | 1.91 | 2661 |
