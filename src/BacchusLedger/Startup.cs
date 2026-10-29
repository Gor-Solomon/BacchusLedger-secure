using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using BacchusLedger.Extensions;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace BacchusLedger;

public sealed class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        services.ConfigureHttpJsonOptions(o => ConfigureJson(o.SerializerOptions));
        services.AddControllers(o =>
            {
                // Same output as the former minimal API: null is written as JSON null, strings as JSON strings.
                o.OutputFormatters.RemoveType<HttpNoContentOutputFormatter>();
                o.OutputFormatters.RemoveType<StringOutputFormatter>();
            })
            .ConfigureApiBehaviorOptions(o => o.SuppressMapClientErrors = true)
            .AddJsonOptions(o => ConfigureJson(o.JsonSerializerOptions));

        services.AddHealthFeature();
        services.AddOverviewFeature();
        services.AddBatchesFeature();
        services.AddVesselsFeature();
        services.AddReadingsFeature();
        services.AddJournalsFeature();
        services.AddReportFeature();
        services.AddSealFeature();
        services.AddAssistantFeature();
        services.AddLiveFeature();
    }

    public void Configure(WebApplication app)
    {
        Db.Init(string.Equals(Environment.GetEnvironmentVariable("RESET_ON_START"), "true", StringComparison.OrdinalIgnoreCase));
        Seal.EnsureAll(); // secure copy: hash chain + DEMO signature for every batch

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapControllers();
    }

    static void ConfigureJson(JsonSerializerOptions o)
    {
        o.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        o.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
    }
}
