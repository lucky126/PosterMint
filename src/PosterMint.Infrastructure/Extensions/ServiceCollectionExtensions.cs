using PosterMint.Application.AI;
using PosterMint.Application.Rendering;
using PosterMint.Application.Configs;
using PosterMint.Application.Sessions;
using PosterMint.Application.Shops;
using PosterMint.Application.Templates;
using PosterMint.Infrastructure.AI;
using PosterMint.Infrastructure.Configs;
using PosterMint.Infrastructure.Persistence;
using PosterMint.Infrastructure.Rendering;
using PosterMint.Infrastructure.Sessions;
using PosterMint.Infrastructure.Shops;
using PosterMint.Infrastructure.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PosterMint.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPosterMintInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("Default") ??
            "Data Source=data/postermint.db";

        EnsureDatabaseFolder(connectionString);

        services.AddDbContext<PosterMintDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddHttpClient();
        services.Configure<LlmOptions>(BindLlmOptions(configuration));
        services.AddSingleton<ISessionInteractionStore, SessionInteractionStore>();
        services.AddScoped<ITemplateService, TemplateService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IConfigService, ConfigService>();
        services.AddScoped<IRenderService, PosterRenderService>();
        services.AddScoped<IChatEditingAiService, ChatEditingAiService>();
        services.AddScoped<IShopService, ShopService>();

        return services;
    }

    private static Action<LlmOptions> BindLlmOptions(IConfiguration configuration) =>
        options =>
        {
            configuration.GetSection("LlmText").Bind(options);

            options.Enabled = ReadBool("LLM_ENABLED", options.Enabled);
            options.Provider = ReadString("LLM_PROVIDER", options.Provider);
            options.BaseUrl = ReadString("LLM_BASE_URL", options.BaseUrl);
            options.ChatUrl = ReadString("LLM_CHAT_URL", options.ChatUrl);
            options.ApiKey = ReadString("LLM_API_KEY", options.ApiKey);
            options.Model = ReadString("LLM_MODEL", options.Model);
            options.TimeoutSeconds = ReadInt("LLM_TIMEOUT_MS", options.TimeoutSeconds * 1000) / 1000;
            options.Temperature = ReadDouble("LLM_TEMPERATURE", options.Temperature);
            options.ResponseFormat = ReadBool("LLM_RESPONSE_FORMAT", options.ResponseFormat);

            if (options.TimeoutSeconds <= 0)
            {
                options.TimeoutSeconds = 60;
            }
        };

    private static void EnsureDatabaseFolder(string connectionString)
    {
        const string prefix = "Data Source=";
        if (!connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var path = connectionString[prefix.Length..].Trim();
        var directory = Path.GetDirectoryName(path);

        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
    }

    private static string ReadString(string key, string currentValue) =>
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key))
            ? currentValue
            : Environment.GetEnvironmentVariable(key)!.Trim();

    private static bool ReadBool(string key, bool currentValue)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        return bool.TryParse(raw, out var value) ? value : currentValue;
    }

    private static int ReadInt(string key, int currentValue)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        return int.TryParse(raw, out var value) ? value : currentValue;
    }

    private static double ReadDouble(string key, double currentValue)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        return double.TryParse(raw, out var value) ? value : currentValue;
    }
}
