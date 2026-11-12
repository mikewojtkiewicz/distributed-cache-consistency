# Cache-Strategy Comparison

## Four caching strategies

- **Cache-aside with invalidation:** Update PostgreSQL, then delete the cached copy. The next read reloads it.
- **Time to Live (TTL):** Update PostgreSQL, but leave the cached copy unchanged until its expiration time. The next read after expiration reloads it from PostgreSQL.
- **Write-through:** Update PostgreSQL, then replace the cached copy immediately.
- **Version-aware:** Update PostgreSQL, then replace the cached copy only if the updated record has a higher version than the cached entry, or no cached entry exists. This prevents older cache writes from overwriting newer values.

## Cache-aside with invalidation

Cache-aside puts the application in charge of checking and filling the cache:

1. Look for the requested record in Redis.
2. If there is a cache hit, return it.
3. If there is a cache miss, read PostgreSQL, put a copy in Redis, and return it.

For updates, the application writes to the database first and then deletes the corresponding cached entry. A later read reloads the updated record.

**Implementation file:** `src/CacheConsistency.Infrastructure/Caching/CacheAsideStrategy.cs`

### Our demo's cache-aside sequence

| Operation | What happens |
| --- | --- |
| First read | Redis is empty; load 100/v1 from PostgreSQL and cache it. |
| Second read | Return 100/v1 from Redis. |
| Write | Update PostgreSQL to 99/v2, then delete the Redis entry. |
| Third read | Reload 99/v2 from PostgreSQL and cache it. |

**Main idea:** Cache-aside fills the cache when data is requested. After a write, it removes the old copy rather than immediately replacing it.

## Time to Live (TTL)

TTL puts the application in charge of checking and filling the cache:

1. Look for the requested record in Redis.
2. If there is a cache hit, return it.
3. If there is a cache miss, read PostgreSQL, put a copy in Redis with an expiration time, and return it.

For updates, the application writes to PostgreSQL but leaves the corresponding cached entry unchanged. After that entry expires, a later read reloads the updated record.

**Implementation file:** `src/CacheConsistency/Infrastructure/Caching/TtlCacheStrategy.cs`

### Our demo's TTL sequence

| Operation | What happens |
| --- | --- |
| First read | Redis is empty; load 100/v1 from PostgreSQL and cache it with a TTL. |
| Second read | Return 100/v1 from Redis. |
| Write | Update PostgreSQL to 99/v2; leave Redis holding 100/v1. |
| Third read | Return stale 100/v1 from Redis. |
| Read after expiration | Reload 99/v2 from PostgreSQL and cache it with a new TTL. |

**Main idea:** TTL caching relies on expiration to remove old data rather than invalidating or replacing it after a write. Reads can return stale data before expiration. Expiration removes the entry; it does not automatically reload it.

## Write-through

Write-through puts the application in charge of checking and filling the cache:

1. Look for the requested record in Redis.
2. If there is a cache hit, return it.
3. If there is a cache miss, read PostgreSQL, put a copy in Redis, and return it.

For updates, the application writes to PostgreSQL first. If the database update succeeds, it replaces the corresponding Redis entry with the updated record.

**Implementation file:** `src/CacheConsistency.Infrastructure/Caching/WriteThroughStrategy.cs`

### Our demo's write-through sequence

| Operation | What happens |
| --- | --- |
| First read | Redis is empty; load 100/v1 from PostgreSQL and cache it. |
| Second read | Return 100/v1 from Redis. |
| Write | Update PostgreSQL to 99/v2, then replace the Redis entry with 99/v2. |
| Third read | Return 99/v2 from Redis. |

**Main idea:** Write-through replaces the cached copy during the write operation, so the next read can retrieve the updated value directly from Redis.

## Version-aware write-through

Version-aware write-through puts the application in charge of checking and filling the cache:

1. Look for the requested record in Redis.
2. If there is a cache hit, return it.
3. If there is a cache miss, read PostgreSQL, attempt to cache the record using a version check, and return the database result.

For updates, the application writes to PostgreSQL first. If successful, it attempts to replace the Redis entry with the updated record. Redis accepts the incoming record only if its version is higher than the cached version, or if no entry exists. The comparison and replacement are performed atomically using a Lua script.

**Implementation file:** `src/CacheConsistency.Infrastructure/Caching/VersionedCacheStrategy.cs`

### Our demo's version-aware write-through sequence

| Operation | What happens |
| --- | --- |
| First read | Redis is empty; load 100/v1 from PostgreSQL and cache it. |
| Second read | Return 100/v1 from Redis. |
| Write | Update PostgreSQL to 99/v2; Redis accepts and caches 99/v2 since v2 is newer. |
| Delayed cache operation | An older operation attempts to cache 100/v1; Redis rejects it because v1 is older than v2. |
| Third read | Return 99/v2 from Redis. |

**Main idea:** Version-aware write-through prevents older cache operations from overwriting newer cached values. Ordinary write-through accepts whichever cache write arrives last, regardless of its version.

## Notes

- Read-through, write-through, and write-behind are several approaches to managing caching, but are not all operations that cache-aside implements.
- In our implementation, the database update and cache replacement are separate operations. A failure between them can leave stale cached data. Concurrent cache writes can also arrive out of order, allowing an older value to overwrite a newer one (the problem our version-aware strategy aims to address).
- Values are shown as stock/version notation; for example, 97/v4 means 97 units in stock at record version 4.
