using HackerNews.BestStories.Domain.Models;

namespace HackerNews.BestStories.Application.Abstraction
{
    public interface IStoryProvider
    {
        Task<IReadOnlyList<int>> GetStoryIdsAsync(CancellationToken cancellationToken);
        Task<Story?> GetStoryByIdAsync(int storyId, CancellationToken cancellationToken);
    }
}
