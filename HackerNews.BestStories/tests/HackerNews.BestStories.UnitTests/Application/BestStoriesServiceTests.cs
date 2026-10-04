using FluentAssertions;
using HackerNews.BestStories.Application.Abstraction;
using HackerNews.BestStories.Application.Options;
using HackerNews.BestStories.Application.Services;
using HackerNews.BestStories.Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace HackerNews.BestStories.UnitTests.Application;

public class BestStoriesServiceTests
{
    private readonly Mock<IStoryProvider> provider = new();

    private static Story Story(int id, int score) =>
        new(id, $"Story {id}", $"https://example.com/{id}", "author", DateTimeOffset.UnixEpoch, score, 3);

    private void GivenStories(params Story?[] stories)
    {
        provider.Setup(p => p.GetStoryIdsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Range(1, stories.Length).ToArray());

        for (var i = 0; i < stories.Length; i++)
        {
            var story = stories[i];
            provider.Setup(p => p.GetStoryByIdAsync(i + 1, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(story);
        }
    }

    private BestStoriesService CreateSut() =>
        new(new MemoryCache(new MemoryCacheOptions()),
            provider.Object,
            Options.Create(new BestStoryOptions
            {
                MaxStoriesToFetch = 200,
                MaxConcurrentRequests = 5,
                BestStoriesCacheDuration = TimeSpan.FromMinutes(1),
                StoryCacheDuration = TimeSpan.FromMinutes(1)
            }),
            NullLogger<BestStoriesService>.Instance);

    [Fact]
    public async Task Returns_top_n_ordered_by_score_descending()
    {
        GivenStories(Story(1, 10), Story(2, 300), Story(3, 50), Story(4, 200));

        var result = await CreateSut().GetBestStoriesAsync(3, default);

        result.Select(s => s.Score).Should().Equal(300, 200, 50);
    }

    [Fact]
    public async Task Returns_all_available_when_n_exceeds_number_of_stories()
    {
        GivenStories(Story(1, 10), Story(2, 20));

        var result = await CreateSut().GetBestStoriesAsync(100, default);

        result.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(201)]
    public async Task Throws_when_count_is_out_of_range(int count)
    {
        var act = () => CreateSut().GetBestStoriesAsync(count, default);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Second_call_is_served_from_cache()
    {
        GivenStories(Story(1, 10), Story(2, 20));
        var sut = CreateSut();

        await sut.GetBestStoriesAsync(2, default);
        await sut.GetBestStoriesAsync(2, default);

        provider.Verify(p => p.GetStoryIdsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Stories_filtered_out_by_the_provider_are_excluded()
    {
        GivenStories(Story(1, 10), null);

        var result = await CreateSut().GetBestStoriesAsync(5, default);

        result.Should().ContainSingle();
    }

    [Fact]
    public async Task A_single_failing_story_does_not_fail_the_request()
    {
        GivenStories(Story(1, 10), Story(2, 20), Story(3, 30));
        provider.Setup(p => p.GetStoryByIdAsync(2, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("boom"));

        var result = await CreateSut().GetBestStoriesAsync(5, default);

        result.Select(s => s.Score).Should().Equal(30, 10);
    }

    [Fact]
    public async Task Throws_when_every_story_fails_and_there_is_no_stale_data()
    {
        GivenStories(Story(1, 10), Story(2, 20));
        provider.Setup(p => p.GetStoryByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("boom"));

        var act = () => CreateSut().GetBestStoriesAsync(5, default);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Concurrent_callers_trigger_a_single_upstream_refresh()
    {
        GivenStories(Story(1, 10), Story(2, 20));
        provider.Setup(p => p.GetStoryIdsAsync(It.IsAny<CancellationToken>()))
                .Returns(async (CancellationToken ct) =>
                {
                    await Task.Delay(100, ct);
                    return [1, 2];
                });
        var sut = CreateSut();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => sut.GetBestStoriesAsync(2, default)));

        provider.Verify(p => p.GetStoryIdsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}