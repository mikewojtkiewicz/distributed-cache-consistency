# CacheConsistency


### How to Run

As this is a containerized environment, run the following commands in your terminal:

1. `docker compose build`
2. `docker compose up`
    - This will run the Benchmarks with the default values specified in `/src/CacheConsistency.Benchmarks/WorkloadOptions.cs`
3. `docker compose down`

If you wish to run the tests yourself on the CLI with `dotnet` commands, the follow command will pull a container and drop you into a shell:

```docker run --rm -it -v "$PWD:/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 bash```

Then run `dotnet test` in the CLI to perform the tests in `/tests`


NOTE: 
To run specific variations of the Benchmarks, see the README in `/src/CacheConsistency.Benchmarks/` for further details. 

Generically specific iterations can be run with the following commands:

1. `docker compose build`

2. `docker compose run --rm app --workers 10 --ratio 90/10 --duration-seconds 20`
