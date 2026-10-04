using HackerNews.BestStories.Application.Abstraction;
using HackerNews.BestStories.Application.Dtos;
using HackerNews.BestStories.Application.Options;
using HackerNews.BestStories.Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace HackerNews.BestStories.Application.Services;

public class BestStoriesService(
    IMemoryCache memoryCache,
    IStoryProvider storyProvider,
    IOptions<BestStoryOptions> options,
    ILogger<BestStoriesService> logger) : IBestStoriesService
{
    private readonly BestStoryOptions bestStoryOptions = options.Value;
    private readonly SemaphoreSlim semaphoreSlim = new(1, 1);
    private const string StoriesByScoreCacheKey = "stories-by-score";
    private volatile IReadOnlyList<Story>? lastKnownBestStories;

    public async Task<IReadOnlyList<StoryDto>> GetBestStoriesAsync(int count, CancellationToken cancellationToken)
    {
        if (count < 1 || count > bestStoryOptions.MaxStoriesToFetch)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, $"Count must be between 1 and {bestStoryOptions.MaxStoriesToFetch}");
        }

        var stories = await GetBestStoriesByScoreAsync(cancellationToken).ConfigureAwait(false);

        return stories.Take(count).Select(StoryDto.FromDomain).ToArray();

    }

    private async Task<IReadOnlyList<Story>> GetBestStoriesByScoreAsync(CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue(StoriesByScoreCacheKey, out IReadOnlyList<Story>? cached) && cached is not null)
        {
            return cached;
        }

        await semaphoreSlim.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (memoryCache.TryGetValue(StoriesByScoreCacheKey, out cached) && cached is not null)
            {
                return cached;
            }

            try
            {
                var stories = await BuildStoriesByScoreAsync(cancellationToken).ConfigureAwait(false);
                memoryCache.Set(StoriesByScoreCacheKey, stories, bestStoryOptions.BestStoriesCacheDuration);
                lastKnownBestStories = stories;
                return stories;
            }
            catch (Exception ex) when (lastKnownBestStories is not null)
            {
                var stale = lastKnownBestStories;

                logger.LogWarning(ex, $"Error occurred while retrieving the stories from upstream. Last Known best stories {stale.Count}");

                return stale;
            }
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }

    private async Task<IReadOnlyList<Story>> BuildStoriesByScoreAsync(CancellationToken cancellationToken)
    {
        var ids = await storyProvider.GetStoryIdsAsync(cancellationToken).ConfigureAwait(false);

        var distinctIds = ids.Distinct().ToArray();

        var stories = new ConcurrentBag<Story>();

        var failures = 0;

        await Parallel.ForEachAsync(
            distinctIds,
            new ParallelOptions { MaxDegreeOfParallelism = bestStoryOptions.MaxConcurrentRequests, CancellationToken = cancellationToken },
            async (id, ct) =>
            {
                try
                {
                    var story = await GetStoryCachedAsync(id, ct).ConfigureAwait(false);

                    if(story is not null)
                    {
                        stories.Add(story);
                    }
                }
                catch (Exception ex) 
                { 
                    Interlocked.Increment(ref failures);
                    logger.LogWarning(ex, $"Failed to retrieve {id}");
                }
            });

        if (failures > 0 && stories.IsEmpty)
        {
            throw new Exception("Failed to retrieve any of the stories");
        }

        return stories.OrderByDescending(s => s.Score).ThenBy(s => s.Id).ToArray();
    }


    private async Task<Story?> GetStoryCachedAsync(int id, CancellationToken cancellationToken)
    {
        var key = StoryCacheKey(id);

        if (memoryCache.TryGetValue(key, out StoryCacheEntry? storyCacheEntry) && storyCacheEntry is not null)
        {
            return storyCacheEntry.story;
        }

        var story = await storyProvider.GetStoryByIdAsync(id, cancellationToken).ConfigureAwait(false);
        memoryCache.Set(key, new StoryCacheEntry(story), bestStoryOptions.StoryCacheDuration);
        return story;
    }

    private static string StoryCacheKey(int id) { return $"story-item-{id}"; }

    private record StoryCacheEntry(Story? story);
}
