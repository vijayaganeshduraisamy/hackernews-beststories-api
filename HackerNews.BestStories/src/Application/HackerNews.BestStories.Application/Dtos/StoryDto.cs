using HackerNews.BestStories.Domain.Models;

namespace HackerNews.BestStories.Application.Dtos
{
    public sealed record StoryDto(
    string Title,
    string Uri,
    string PostedBy,
    DateTimeOffset Time,
    int Score,
    int CommentCount)
    {
        public static StoryDto FromDomain(Story story)
        {
            return new StoryDto(
                    story.Title,
                    story.Uri,
                    story.PostedBy,
                    story.Time,
                    story.Score,
                    story.CommentCount
                );
        }
    }
}
