using Microsoft.Extensions.Options;
using TradeFeed.Publisher;
using TradeFeed.Publisher.Publishing;
using TradeFeed.Publisher.Sources;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<PublisherOptions>()
    .Bind(builder.Configuration.GetSection(PublisherOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        o => o.Symbols.All(s => s.Length is >= 2 and <= 20 && s.All(char.IsAsciiLetterOrDigit)),
        "Publisher:Symbols must be 2-20 character alphanumeric symbols such as BTCUSDT.")
    .ValidateOnStart();

builder.Services.AddSingleton<ITradeSource>(sp =>
    sp.GetRequiredService<IOptions<PublisherOptions>>().Value.Source switch
    {
        TradeSourceType.Simulated => ActivatorUtilities.CreateInstance<SimulatedTradeSource>(sp),
        _ => ActivatorUtilities.CreateInstance<BinanceTradeSource>(sp),
    });

builder.Services.AddSingleton<ITradeHubClient>(sp => new SignalRTradeHubClient(
    sp.GetRequiredService<IOptions<PublisherOptions>>().Value,
    sp.GetRequiredService<ILogger<SignalRTradeHubClient>>()));

builder.Services.AddHostedService<TradePublisherWorker>();

builder.Build().Run();
