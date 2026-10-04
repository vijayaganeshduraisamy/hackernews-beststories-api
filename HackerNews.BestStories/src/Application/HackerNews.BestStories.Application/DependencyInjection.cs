using HackerNews.BestStories.Application.Abstraction;
using HackerNews.BestStories.Application.Options;
using HackerNews.BestStories.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HackerNews.BestStories.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {

        services.AddOptions<BestStoryOptions>()
            .Bind(configuration.GetSection(BestStoryOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddMemoryCache();
        services.AddSingleton<IBestStoriesService, BestStoriesService>();

        return services;
    }
}
