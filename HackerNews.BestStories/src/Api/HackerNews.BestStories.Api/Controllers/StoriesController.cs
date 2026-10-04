using HackerNews.BestStories.Application.Abstraction;
using HackerNews.BestStories.Application.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;

namespace HackerNews.BestStories.Api.Controllers;


[ApiController]
[Route("api/stories")]
[EnableRateLimiting("beststories")]
public class StoriesController (IBestStoriesService bestStoriesService) : ControllerBase
{
    [HttpGet("best")]
    [OutputCache(PolicyName = "BestStories")]
    [ProducesResponseType(typeof(IReadOnlyList<StoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<StoryDto>>> GetBestStories([FromQuery] int n = 10, CancellationToken cancellationToken = default)
    {
        var stories = await bestStoriesService.GetBestStoriesAsync(n, cancellationToken);
        return Ok(stories);
    }
}
