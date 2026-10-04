using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using HackerNews.BestStories.Infrastructure.HackerNewsApi;
using HackerNews.BestStories.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace HackerNews.BestStories.UnitTests;

public class HackerNewsBestStoryProviderTests
{
    private sealed class StubHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> RequestedUrls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestedUrls.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(respond());
        }
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Status(HttpStatusCode code) => new(code);

    private static (HackerNewsBestStoryProvider Sut, StubHandler Handler) Create(Func<HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var options = Options.Create(new HackerNewsApiOptions
        {
            BaseUrl = "https://test.local/v0/",
            StoriesPath = "beststories.json",
            ItemPathFormat = "item/{0}.json"
        });

        var client = new HttpClient(handler) { BaseAddress = new Uri(options.Value.BaseUrl) };

        return (new HackerNewsBestStoryProvider(client, options), handler);
    }

    private static string ItemJson(
        string type = "story",
        string? title = "A title",
        long time = 1570887781,
        string extra = "") =>
        "{\"id\":21233041,\"by\":\"vijay\",\"score\":1716,\"descendants\":572," +
        $"\"type\":\"{type}\",\"time\":{time}," +
        (title is null ? "" : $"\"title\":\"{title}\",") +
        "\"url\":\"https://example.com/a\"," + extra + "\"kids\":[1,2]}";

    [Fact]
    public async Task GetStoryIds_returns_ids_from_the_configured_path()
    {
        var (sut, handler) = Create(() => Json("[3,2,1]"));

        var ids = await sut.GetStoryIdsAsync(default);

        ids.Should().Equal(3, 2, 1);
        handler.RequestedUrls.Should().ContainSingle().Which.Should().Be("https://test.local/v0/beststories.json");
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    public async Task GetStoryIds_throws_when_the_list_is_empty_or_null(string body)
    {
        var (sut, _) = Create(() => Json(body));

        var act = () => sut.GetStoryIdsAsync(default);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetStoryIds_throws_on_error_status(HttpStatusCode code)
    {
        var (sut, _) = Create(() => Status(code));

        var act = () => sut.GetStoryIdsAsync(default);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetStoryIds_throws_on_malformed_json()
    {
        var (sut, _) = Create(() => Json("{not json"));

        var act = () => sut.GetStoryIdsAsync(default);

        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task GetStoryIds_propagates_cancellation()
    {
        var (sut, _) = Create(() => Json("[1]"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => sut.GetStoryIdsAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetStoryById_maps_all_fields()
    {
        var (sut, handler) = Create(() => Json(ItemJson()));

        var story = await sut.GetStoryByIdAsync(21233041, default);

        story.Should().NotBeNull();
        story!.Id.Should().Be(21233041);
        story.Title.Should().Be("A title");
        story.Uri.Should().Be("https://example.com/a");
        story.PostedBy.Should().Be("vijay");
        story.Time.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1570887781));
        story.Score.Should().Be(1716);
        story.CommentCount.Should().Be(572);
        handler.RequestedUrls.Should().ContainSingle().Which.Should().Be("https://test.local/v0/item/21233041.json");
    }

    [Fact]
    public async Task GetStoryById_returns_null_on_404()
    {
        var (sut, _) = Create(() => Status(HttpStatusCode.NotFound));

        (await sut.GetStoryByIdAsync(1, default)).Should().BeNull();
    }

    [Fact]
    public async Task GetStoryById_returns_null_when_body_is_json_null()
    {
        // Hacker News returns the literal `null` for unknown ids
        var (sut, _) = Create(() => Json("null"));

        (await sut.GetStoryByIdAsync(1, default)).Should().BeNull();
    }

    [Theory]
    [InlineData("comment")]
    [InlineData("job")]
    [InlineData("poll")]
    public async Task GetStoryById_returns_null_for_non_story_types(string type)
    {
        var (sut, _) = Create(() => Json(ItemJson(type: type)));

        (await sut.GetStoryByIdAsync(1, default)).Should().BeNull();
    }

    [Fact]
    public async Task GetStoryById_matches_the_story_type_case_insensitively()
    {
        var (sut, _) = Create(() => Json(ItemJson(type: "STORY")));

        (await sut.GetStoryByIdAsync(1, default)).Should().NotBeNull();
    }

    [Theory]
    [InlineData("\"deleted\":true,")]
    [InlineData("\"dead\":true,")]
    public async Task GetStoryById_returns_null_for_deleted_or_dead_items(string flag)
    {
        var (sut, _) = Create(() => Json(ItemJson(extra: flag)));

        (await sut.GetStoryByIdAsync(1, default)).Should().BeNull();
    }

    [Theory]
    [InlineData("\"deleted\":false,")]
    [InlineData("\"dead\":false,")]
    public async Task GetStoryById_keeps_items_explicitly_flagged_false(string flag)
    {
        var (sut, _) = Create(() => Json(ItemJson(extra: flag)));

        (await sut.GetStoryByIdAsync(1, default)).Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetStoryById_returns_null_when_title_is_missing_or_blank(string? title)
    {
        var (sut, _) = Create(() => Json(ItemJson(title: title)));

        (await sut.GetStoryByIdAsync(1, default)).Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetStoryById_returns_null_when_time_is_not_positive(long time)
    {
        var (sut, _) = Create(() => Json(ItemJson(time: time)));

        (await sut.GetStoryByIdAsync(1, default)).Should().BeNull();
    }

    [Fact]
    public async Task GetStoryById_defaults_comment_count_to_zero_when_descendants_is_missing()
    {
        const string json =
            "{\"id\":1,\"type\":\"story\",\"by\":\"a\",\"score\":5,\"time\":1570887781,\"title\":\"t\",\"url\":\"https://x\"}";
        var (sut, _) = Create(() => Json(json));

        var story = await sut.GetStoryByIdAsync(1, default);

        story!.CommentCount.Should().Be(0);
    }

    [Fact]
    public async Task GetStoryById_returns_empty_uri_and_author_when_missing()
    {
        const string json =
            "{\"id\":1,\"type\":\"story\",\"score\":5,\"time\":1570887781,\"title\":\"some title\"}";
        var (sut, _) = Create(() => Json(json));

        var story = await sut.GetStoryByIdAsync(1, default);

        story.Should().NotBeNull();
        story!.Uri.Should().BeEmpty();
        story.PostedBy.Should().BeEmpty();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task GetStoryById_throws_on_error_status(HttpStatusCode code)
    {
        var (sut, _) = Create(() => Status(code));

        var act = () => sut.GetStoryByIdAsync(1, default);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetStoryById_throws_on_malformed_json()
    {
        var (sut, _) = Create(() => Json("{not json"));

        var act = () => sut.GetStoryByIdAsync(1, default);

        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task GetStoryById_propagates_cancellation()
    {
        var (sut, _) = Create(() => Json(ItemJson()));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => sut.GetStoryByIdAsync(1, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}