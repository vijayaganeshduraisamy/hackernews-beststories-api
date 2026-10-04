using HackerNews.BestStories.Application.Abstraction;
using HackerNews.BestStories.Infrastructure.HackerNewsApi;
using HackerNews.BestStories.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace HackerNews.BestStories.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<HackerNewsApiOptions>()
            .Bind(configuration.GetSection(HackerNewsApiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<IStoryProvider, HackerNewsBestStoryProvider>((storyProvider, httpClient) =>
        {
            var options = storyProvider.GetRequiredService<IOptions<HackerNewsApiOptions>>().Value;
            httpClient.BaseAddress = new Uri(options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + '/');
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        })
        .AddResilienceHandler("hacker-news", builder =>
        {
            builder
                .AddTimeout(TimeSpan.FromSeconds(15))
                .AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromMilliseconds(200),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true
                })
                .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    FailureRatio = 0.5,
                    MinimumThroughput = 20,
                    BreakDuration = TimeSpan.FromSeconds(30)
                });
        });

        return services;
    }
}
