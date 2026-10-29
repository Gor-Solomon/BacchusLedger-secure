using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using BacchusLedger.Application;
using BacchusLedger.Application.Options;
using BacchusLedger.Application.Repositories;
using BacchusLedger.Application.Services;
using BacchusLedger.Infrastructure;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Options;

namespace BacchusLedger.Api;

public sealed class Startup(IConfiguration configuration)
{
    public void ConfigureServices(IServiceCollection services)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        services.AddControllers(o =>
            {
                // Same output as the former minimal API: null is written as JSON null, strings as JSON strings.
                o.OutputFormatters.RemoveType<HttpNoContentOutputFormatter>();
                o.OutputFormatters.RemoveType<StringOutputFormatter>();
            })
            .ConfigureApiBehaviorOptions(o => o.SuppressMapClientErrors = true)
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
                o.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
            });

        services.Configure<StorageOptions>(o =>
        {
            if (Env("DB_PATH") is { } db) o.DbPath = db;
            o.ResetOnStart = string.Equals(Env("RESET_ON_START"), "true", StringComparison.OrdinalIgnoreCase);
            if (Env("SIGNING_KEY_PATH") is { } key) o.SigningKeyPath = key;
        });
        services.Configure<AssistantOptions>(o =>
        {
            o.OpenAiKey = Env("OPENAI_API_KEY");
            o.GeminiKey = Env("GEMINI_API_KEY");
            o.OpenAiModel = Env("OPENAI_MODEL") ?? o.OpenAiModel;
            o.GeminiModel = Env("GEMINI_MODEL") ?? o.GeminiModel;
            o.Provider = string.Equals(Env("ASSISTANT_PROVIDER"), "gemini", StringComparison.OrdinalIgnoreCase) ? "gemini" : "openai";
            o.Enabled = !string.Equals(Env("ASSISTANT_ENABLED"), "false", StringComparison.OrdinalIgnoreCase);
        });
        services.Configure<LiveOptions>(configuration.GetSection("Live"));

        services.AddApplication();
        services.AddInfrastructure();
    }

    public void Configure(WebApplication app)
    {
        var storage = app.Services.GetRequiredService<IOptions<StorageOptions>>().Value;
        app.Services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(storage.ResetOnStart).GetAwaiter().GetResult();
        app.Services.GetRequiredService<ISealService>().SealAllAsync().GetAwaiter().GetResult(); // hash chain + DEMO signature for every batch

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapControllers();
    }

    /// An environment variable (or other configuration value) with surrounding spaces removed; null when empty.
    string? Env(string key) => configuration[key] is { } v && v.Trim().Length > 0 ? v.Trim() : null;
}
