using ConfigCat.Client;
using ConfigCatLogging;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog();

builder.Services.AddSingleton<IConfigCatClient>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ConfigCatClient>>();

    var client = ConfigCatClient.Get(builder.Configuration.GetValue<string>("SdkKey")!, options =>
    {
        options.Logger = new ConfigCatToMSLoggerAdapter(logger);
    });

    return client;
});

var app = builder.Build();

app.UseSerilogRequestLogging();

await using var scope = app.Services.CreateAsyncScope();
var cc = scope.ServiceProvider.GetRequiredService<IConfigCatClient>();
var ff = await cc.GetValueAsync("foo", false);

app.Run();