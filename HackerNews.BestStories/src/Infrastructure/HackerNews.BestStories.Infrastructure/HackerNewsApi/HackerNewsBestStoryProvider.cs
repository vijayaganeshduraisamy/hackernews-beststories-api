using HackerNews.BestStories.Application.Abstraction;
using HackerNews.BestStories.Domain.Models;
using HackerNews.BestStories.Infrastructure.Options;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;

namespace HackerNews.BestStories.Infrastructure.HackerNewsApi;

public sealed class HackerNewsBestStoryProvider(
    HttpClient httpClient,
    IOptions<HackerNewsApiOptions> options) : IStoryProvider
{
    private readonly HackerNewsApiOptions hackerNewsApiOptions = options.Value;
    private const string StoryType = "story";

    public async Task<IReadOnlyList<int>> GetStoryIdsAsync(CancellationToken cancellationToken)
    {
        var ids = await SendAsync<IReadOnlyList<int>>(hackerNewsApiOptions.StoriesPath, cancellationToken).ConfigureAwait(false);

        return ids is { Count: > 0} ? ids : throw new HttpRequestException("Hacker News API returned no story Ids....");
    }

    public async Task<Story?> GetStoryByIdAsync(int storyId, CancellationToken cancellationToken)
    {
        var path = string.Format(CultureInfo.InvariantCulture, hackerNewsApiOptions.ItemPathFormat, storyId);
        var item = await SendAsync<HackerNewsApiItem>(path, cancellationToken).ConfigureAwait(false);
        return Map(item);
    }

    private static Story? Map(HackerNewsApiItem? hackerNewsApiItem)
    {
        if (hackerNewsApiItem is null
            || hackerNewsApiItem.Deleted == true 
            || hackerNewsApiItem.Dead == true 
            || !string.Equals(hackerNewsApiItem.Type, StoryType, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(hackerNewsApiItem.Title)
            || hackerNewsApiItem.Time <= 0)
        {
            return null;
        }

        return new Story(
            hackerNewsApiItem.Id,
            hackerNewsApiItem.Title ?? string.Empty,
            hackerNewsApiItem.Url ?? string.Empty,
            hackerNewsApiItem.By ?? string.Empty,
            DateTimeOffset.FromUnixTimeSeconds(hackerNewsApiItem.Time),
            hackerNewsApiItem.Score,
            hackerNewsApiItem.Descendants ?? 0);
    }

    private async Task<T?> SendAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
    }
}
