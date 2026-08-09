using Microsoft.Extensions.Options;
using SyncAgent;
using SyncAgent.Api;
using SyncAgent.Configuration;
using SyncAgent.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<SyncPlatformOptions>()
    .Bind(builder.Configuration.GetSection(
        SyncPlatformOptions.SectionName))
    .Validate(
        options => Uri.TryCreate(
            options.BaseUrl,
            UriKind.Absolute,
            out _),
        "SyncPlatform BaseUrl must be a valid absolute URL.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ApiKey),
        "SyncPlatform ApiKey is required.")
    .Validate(
        options => options.PollingIntervalSeconds > 0,
        "PollingIntervalSeconds must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddHttpClient<
    ISyncPlatformClient,
    SyncPlatformClient>(
    (serviceProvider, client) =>
    {
        var options = serviceProvider
            .GetRequiredService<IOptions<SyncPlatformOptions>>()
            .Value;

        client.BaseAddress = new Uri(options.BaseUrl);

        client.DefaultRequestHeaders.Add(
            "X-Api-Key",
            options.ApiKey);

        client.Timeout = TimeSpan.FromSeconds(30);
    });

builder.Services.AddSingleton<
    ISyncTaskDispatcher,
    SyncTaskDispatcher>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();