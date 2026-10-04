using HackerNews.BestStories.Application.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace HackerNews.BestStories.Application.Abstraction;

public interface IBestStoriesService
{
    Task<IReadOnlyList<StoryDto>> GetBestStoriesAsync(int count, CancellationToken cancellationToken);
}
