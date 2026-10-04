using System;
using System.Collections.Generic;
using System.Text;

namespace HackerNews.BestStories.Application.Options
{
    public class BestStoryOptions
    {
        public const string SectionName = "BestStories";
        public TimeSpan StoryCacheDuration { get; set; } = TimeSpan.FromMinutes(5);
        public int MaxConcurrentRequests { get; set; } = 8;

        public TimeSpan BestStoriesCacheDuration { get; set; } = TimeSpan.FromMinutes(1);
        public int MaxStoriesToFetch { get; set; } = 200;
    }

}
