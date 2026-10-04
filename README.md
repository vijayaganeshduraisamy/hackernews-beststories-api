# Hacker News Best Stories API

ASP.NET Core (.NET 10) REST API that returns the best *n* Hacker News stories, ordered by score (highest first).

## Run

```bash
dotnet run --project src/HackerNews.BestStories.Api
```

The Swagger page opens by default, and the endpoint can be tried from there.

API URL: `https://localhost:7103/api/stories/best?n=10`

## Test

```bash
dotnet test
```

Unit tests (xunit, Moq, FluentAssertions) and integration tests (Hacker News mocked, no network calls).

## API

`GET /api/stories/best?n={1..200}` (default 10)

| Status | Meaning |
|--------|---------|
| 200 | Up to `n` stories, highest score first |
| 400 | `n` invalid or outside 1-200 |
| 429 | Rate limit exceeded |
| 500 | Unhandled error (returned by the global error middleware) |

```json
[
  {
    "title": "A uBlock Origin update was rejected from the Chrome Web Store",
    "uri": "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
    "postedBy": "ismaildonmez",
    "time": "2019-10-12T13:43:01+00:00",
    "score": 1716,
    "commentCount": 572
  }
]
```

## Layer Summary

```
src/
    Domain          Story entity
    Application     IBestStoriesService, IStoryProvider, Dtos, Options, BestStoriesService (caching)
    Infrastructure  External integration, HackerNewsBestStoryProvider (HttpClient) + Polly resilience pipeline
    Api             Controller, global exception handler, rate limiting, output cache, swagger, health
tests/
    UnitTests
    IntegrationTests
```

## Features

- Swagger UI
- Rate limiting
- Output caching
- Health check
- Global error handling middleware
- Parallel data retrieval with throttling
- Polly retry and resilience patterns (timeouts, retry with backoff and jitter, circuit breaker)

## Protecting the Hacker News API

- Output cache serves repeated requests without reaching the service.
- The sorted list and each story are cached in memory. Filtered-out stories are cached too.
- Only one refresh runs at a time, so concurrent callers don't each call Hacker News.
- Stories are fetched in parallel, limited by `MaxConcurrentRequests`.
- Retries, timeouts and a circuit breaker on the HTTP client.
- If a refresh fails, the last good list is served. A single failed story is skipped.

## Assumptions

- "Best" is the id list from `beststories.json`, re-sorted by score descending (ties by id).
- Only non-deleted, non-dead items of type `story` are returned.
- `n` above 200 is rejected, since Hacker News returns at most 200 ids.
- Data may be slightly stale, up to the cache durations.
- In theory, top-N over a very large dataset should use a bounded min-heap (O(M log N)). Here the dataset is small (at most 200 stories) and network latency dominates, so a full sort is simpler and entirely acceptable. The focus is on caching and reducing external HTTP calls rather than sorting complexity.

## With more time

- Distributed cache
- Background cache refresh
- Observability and monitoring (OpenTelemetry, Application Insights)
- API versioning
- Containerisation and cloud deployment
- Security hardening