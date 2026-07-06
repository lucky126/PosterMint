using PosterMint.Infrastructure.Extensions;
using PosterMint.Infrastructure.Persistence;
using PosterMint.Web.Components;
using PosterMint.Web.Endpoints;
using System.Text.Json.Serialization;

namespace PosterMint.Web;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services.AddPosterMintInfrastructure(builder.Configuration);
        builder.Services.AddHealthChecks();
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }

        app.UseStaticFiles();
        app.UseAntiforgery();

        await app.Services.InitializeDatabaseAsync();

        app.MapPosterMintApi();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        await app.RunAsync();
    }
}
