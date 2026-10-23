# CacheConsistency


### How to Run
As this is a containerized environment, run the following commands in your terminal:

1. `docker compose build`
2. `docker compose up`
    - This will run the Benchmarks with the default values specified in `/src/CacheConsistency.Benchmarks/WorkloadOptions.cs`
3. `docker compose down`

NOTE: 
To run specific variations of the Benchmarks, see the README in `/src/CacheConsistency.Benchmarks/` for further details. 

Generically specific iterations can be run with the following commands:

1. `docker compose build`

2. `docker compose run --rm app --workers 10 --ratio 90/10 --duration-seconds 20`
