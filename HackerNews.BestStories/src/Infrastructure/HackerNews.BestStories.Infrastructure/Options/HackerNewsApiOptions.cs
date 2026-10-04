namespace HackerNews.BestStories.Infrastructure.Options;

public sealed class HackerNewsApiOptions
{
    public const string SectionName = "HackerNewsApi";

    public required string BaseUrl { get; set; }

    public required string StoriesPath { get; set; }

    public required string ItemPathFormat { get; set; }

}
