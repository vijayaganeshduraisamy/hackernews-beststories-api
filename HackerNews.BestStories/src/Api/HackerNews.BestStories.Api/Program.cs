using HackerNews.BestStories.Api.Middleware;
using HackerNews.BestStories.Application;
using HackerNews.BestStories.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter(
        "beststories",
        limiterOptions =>
        {
            limiterOptions.PermitLimit = 60;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.QueueLimit = 0;
        });

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        await context.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                Error = "Too many requests. Please try again later."
            },
            token);
    };
});

builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("BestStories", policy =>
    {
        policy.Expire(TimeSpan.FromMinutes(1))
        .SetVaryByQuery("n");
    });
});


var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseOutputCache();

app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapControllers();

app.Run();