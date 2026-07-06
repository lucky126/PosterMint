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

        // 根路径 302 到后台商铺列表；这里比 Blazor Router 更早注册，因此 Router 不会看到 "/"。
        // 用普通 endpoint 而不是 Razor 组件里的 NavigateTo，是为了避开 Blazor 预渲染阶段
        // 主动调 NavigateTo 会抛 NavigationException 的已知行为。
        app.MapGet("/", () => Results.Redirect("/admin/shops"));

        app.MapPosterMintApi();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        await app.RunAsync();
    }
}
