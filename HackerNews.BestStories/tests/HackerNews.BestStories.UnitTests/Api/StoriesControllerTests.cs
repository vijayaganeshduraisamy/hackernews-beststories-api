using HackerNews.BestStories.Api.Controllers;
using HackerNews.BestStories.Application.Abstraction;
using HackerNews.BestStories.Application.Dtos;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace HackerNews.BestStories.UnitTests.Api;

public class StoriesControllerTests
{
    [Fact]
    public async Task Returns_200_and_passes_n_to_the_service()
    {
        var expected = new[]
        {
            new StoryDto("t", "u", "p", DateTimeOffset.UnixEpoch, 1, 2)
        };

        var service = new Mock<IBestStoriesService>();
        service.Setup(s => s.GetBestStoriesAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var sut = new StoriesController(service.Object);

        var result = await sut.GetBestStories(5, default);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeSameAs(expected);
        service.Verify(s => s.GetBestStoriesAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }
}
