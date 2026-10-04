using FluentAssertions;
using HackerNews.BestStories.Application.Abstraction;
using HackerNews.BestStories.Domain.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HackerNews.BestStories.IntegrationTests;

public class StoriesControllerIntegrationTests
{
    private const string Url = "/api/stories/best";

    private sealed class ApiFactory(Mock<IStoryProvider> provider) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IStoryProvider>();
                services.AddSingleton(provider.Object);
            });
    }

    private static Mock<IStoryProvider> ProviderWith(params (int Id, int Score)[] items)
    {
        var stories = items.ToDictionary(
            i => i.Id,
            i => new Story(i.Id, $"Story {i.Id}", $"https://example.com/{i.Id}", "author",
                           DateTimeOffset.FromUnixTimeSeconds(1570887781), i.Score, 7));

        var provider = new Mock<IStoryProvider>();
        provider.Setup(p => p.GetStoryIdsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(items.Select(i => i.Id).ToArray());
        provider.Setup(p => p.GetStoryByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int id, CancellationToken _) => stories.GetValueOrDefault(id));
        return provider;
    }

    [Fact]
    public async Task Get_best_returns_200_with_stories_in_descending_score_order()
    {
        await using var factory = new ApiFactory(ProviderWith((1, 10), (2, 300), (3, 50)));
        var client = factory.CreateClient();

        var response = await client.GetAsync($"{Url}?n=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        body.Should().NotBeNull();
        body!.Select(s => s.GetProperty("score").GetInt32()).Should().Equal(300, 50);
    }

    [Fact]
    public async Task Get_best_returns_the_contract_shape()
    {
        await using var factory = new ApiFactory(ProviderWith((1, 10)));
        var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<JsonElement[]>($"{Url}?n=1");

        var story = body.Should().ContainSingle().Subject;
        story.GetProperty("title").GetString().Should().Be("Story 1");
        story.GetProperty("uri").GetString().Should().Be("https://example.com/1");
        story.GetProperty("postedBy").GetString().Should().Be("author");
        story.GetProperty("time").GetString().Should().Be("2019-10-12T13:43:01+00:00");
        story.GetProperty("score").GetInt32().Should().Be(10);
        story.GetProperty("commentCount").GetInt32().Should().Be(7);
    }

    [Fact]
    public async Task Get_best_defaults_to_10_stories_when_n_is_omitted()
    {
        var items = Enumerable.Range(1, 15).Select(i => (i, i)).ToArray();
        await using var factory = new ApiFactory(ProviderWith(items));
        var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<JsonElement[]>(Url);

        body.Should().HaveCount(10);
    }

    [Fact]
    public async Task Get_best_returns_everything_available_when_n_exceeds_the_number_of_stories()
    {
        await using var factory = new ApiFactory(ProviderWith((1, 10), (2, 20)));
        var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<JsonElement[]>($"{Url}?n=100");

        body.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("201")]
    [InlineData("abc")]
    public async Task Get_best_returns_400_for_an_invalid_n(string n)
    {
        await using var factory = new ApiFactory(ProviderWith((1, 10)));
        var client = factory.CreateClient();

        var response = await client.GetAsync($"{Url}?n={n}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Repeated_requests_do_not_hit_the_upstream_provider_again()
    {
        var provider = ProviderWith((1, 10), (2, 20));
        await using var factory = new ApiFactory(provider);
        var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            (await client.GetAsync($"{Url}?n=2")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        provider.Verify(p => p.GetStoryIdsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Concurrent_requests_trigger_a_single_upstream_refresh()
    {
        var provider = ProviderWith((1, 10), (2, 20), (3, 30));
        await using var factory = new ApiFactory(provider);
        var client = factory.CreateClient();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => client.GetAsync($"{Url}?n=3")));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        provider.Verify(p => p.GetStoryIdsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}