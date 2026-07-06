using PosterMint.Application.AI;
using PosterMint.Application.Configs;
using PosterMint.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace PosterMint.Infrastructure.Configs;

public sealed class ConfigService(
    PosterMintDbContext dbContext,
    IHttpClientFactory httpClientFactory) : IConfigService
{
    public async Task<IReadOnlyList<ConfigEntryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var configs = await dbContext.ConfigEntries
            .AsNoTracking()
            .OrderBy(x => x.ConfigGroup)
            .ThenBy(x => x.ConfigKey)
            .ToListAsync(cancellationToken);

        return configs.Select(MapEntry).ToList();
    }

    public async Task<AiConfigurationDto> GetAiConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var configs = await dbContext.ConfigEntries
            .AsNoTracking()
            .Where(x => x.ConfigGroup == "AI")
            .ToListAsync(cancellationToken);

        return new AiConfigurationDto
        {
            TextProvider = GetValue(configs, "LlmText:Provider", "openai-compatible"),
            TextBaseUrl = GetValue(configs, "LlmText:BaseUrl"),
            TextChatUrl = GetValue(configs, "LlmText:ChatUrl"),
            TextApiKey = GetValue(configs, "LlmText:ApiKey"),
            TextModel = GetValue(configs, "LlmText:Model"),
            ImageProvider = GetValue(configs, "LlmImage:Provider", "openai-compatible"),
            ImageBaseUrl = GetValue(configs, "LlmImage:BaseUrl"),
            ImageApiKey = GetValue(configs, "LlmImage:ApiKey"),
            ImageModel = GetValue(configs, "LlmImage:Model")
        };
    }

    public async Task<AiConfigurationStatusDto> GetAiConfigurationStatusAsync(CancellationToken cancellationToken = default)
    {
        var config = await GetAiConfigurationAsync(cancellationToken);

        var textConfigured =
            !string.IsNullOrWhiteSpace(config.TextBaseUrl) &&
            !string.IsNullOrWhiteSpace(config.TextApiKey) &&
            !string.IsNullOrWhiteSpace(config.TextModel);

        var imageConfigured =
            !string.IsNullOrWhiteSpace(config.ImageBaseUrl) &&
            !string.IsNullOrWhiteSpace(config.ImageApiKey) &&
            !string.IsNullOrWhiteSpace(config.ImageModel);

        return new AiConfigurationStatusDto(textConfigured, imageConfigured, textConfigured && imageConfigured);
    }

    public async Task SaveAiConfigurationAsync(AiConfigurationDto request, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        // 自愈：MiniMax 文生图旧路径 /v1/image/generations 已废弃，纠正为官方 /v1/image_generation
        var imageBaseUrl = request.ImageBaseUrl;
        if (request.ImageProvider.Equals("minimax", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(imageBaseUrl) &&
            (imageBaseUrl.Contains("/image/generations", StringComparison.OrdinalIgnoreCase) ||
             imageBaseUrl.Contains("/images/generations", StringComparison.OrdinalIgnoreCase)))
        {
            imageBaseUrl = imageBaseUrl
                .Replace("/images/generations", "/image_generation", StringComparison.OrdinalIgnoreCase)
                .Replace("/image/generations", "/image_generation", StringComparison.OrdinalIgnoreCase);
        }

        await UpsertAsync("LlmText:Provider", request.TextProvider, false, "文本模型提供方", now, cancellationToken);
        await UpsertAsync("LlmText:BaseUrl", request.TextBaseUrl, false, "文本模型基础地址", now, cancellationToken);
        await UpsertAsync("LlmText:ChatUrl", request.TextChatUrl, false, "文本模型聊天地址", now, cancellationToken);
        await UpsertAsync("LlmText:ApiKey", request.TextApiKey, true, "文本模型密钥", now, cancellationToken);
        await UpsertAsync("LlmText:Model", request.TextModel, false, "文本模型名称", now, cancellationToken);

        await UpsertAsync("LlmImage:Provider", request.ImageProvider, false, "文生图模型提供方", now, cancellationToken);
        await UpsertAsync("LlmImage:BaseUrl", imageBaseUrl, false, "文生图模型基础地址", now, cancellationToken);
        await UpsertAsync("LlmImage:ApiKey", request.ImageApiKey, true, "文生图模型密钥", now, cancellationToken);
        await UpsertAsync("LlmImage:Model", request.ImageModel, false, "文生图模型名称", now, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertAsync(
        string key,
        string value,
        bool isSecret,
        string description,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.ConfigEntries.FirstOrDefaultAsync(x => x.ConfigKey == key, cancellationToken);
        if (entity is null)
        {
            dbContext.ConfigEntries.Add(new Domain.Entities.ConfigEntryEntity
            {
                ConfigKey = key,
                ConfigGroup = "AI",
                ConfigValue = value,
                IsSecret = isSecret,
                Description = description,
                UpdatedAt = now
            });
            return;
        }

        entity.ConfigValue = value;
        entity.IsSecret = isSecret;
        entity.Description = description;
        entity.UpdatedAt = now;
    }

    private static ConfigEntryDto MapEntry(Domain.Entities.ConfigEntryEntity entry) =>
        new(
            entry.ConfigKey,
            entry.ConfigGroup,
            entry.IsSecret ? "******" : entry.ConfigValue,
            entry.IsSecret,
            entry.Description,
            entry.UpdatedAt);

    private static string GetValue(
        IReadOnlyCollection<Domain.Entities.ConfigEntryEntity> entries,
        string key,
        string fallback = "") =>
        entries.FirstOrDefault(x => x.ConfigKey == key)?.ConfigValue ?? fallback;

    public async Task<AiTestResultDto> TestTextModelAsync(CancellationToken cancellationToken = default)
    {
        var config = await GetAiConfigurationAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(config.TextBaseUrl) ||
            string.IsNullOrWhiteSpace(config.TextApiKey) ||
            string.IsNullOrWhiteSpace(config.TextModel))
        {
            return new AiTestResultDto(
                false,
                config.TextModel,
                0,
                "请先填写 Base URL、API Key 和 Model");
        }

        var baseUrl = config.TextBaseUrl.TrimEnd('/');
        var chatUrl = !string.IsNullOrWhiteSpace(config.TextChatUrl)
            ? config.TextChatUrl
            : baseUrl.Contains("/chatcompletion", StringComparison.OrdinalIgnoreCase) ||
              baseUrl.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? baseUrl
            : $"{baseUrl}/chat/completions";

        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", config.TextApiKey);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var body = new JsonObject
            {
                ["model"] = config.TextModel,
                ["messages"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["role"] = "user",
                        ["content"] = "回复一个字：是"
                    }
                },
                ["max_tokens"] = 10,
                ["temperature"] = 0.1,
                ["stream"] = false
            };

            // MiniMax chatcompletion_v2 是 OpenAI 兼容格式，使用 Bearer + 标准 messages，
            // 不再注入旧版 chatcompletion_pro 的 bot_setting。

            using var content = new StringContent(
                body.ToJsonString(),
                Encoding.UTF8,
                "application/json");

            using var response = await client.PostAsync(chatUrl, content, cancellationToken);

            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                using var document = System.Text.Json.JsonDocument.Parse(responseContent);
                var responseMessage = document.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                return new AiTestResultDto(
                    true,
                    config.TextModel,
                    (int)stopwatch.ElapsedMilliseconds,
                    $"连接成功，模型回复：{responseMessage}");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return new AiTestResultDto(
                    false,
                    config.TextModel,
                    (int)stopwatch.ElapsedMilliseconds,
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}",
                    errorContent);
            }
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return new AiTestResultDto(
                false,
                config.TextModel,
                (int)stopwatch.ElapsedMilliseconds,
                $"连接失败：{FlattenError(exception)}",
                exception.ToString());
        }
    }

    private static string FlattenError(Exception exception)
    {
        var messages = new List<string>();
        var current = exception;
        while (current is not null)
        {
            messages.Add($"{current.GetType().Name}: {current.Message}");
            current = current.InnerException;
        }

        return string.Join(" -> ", messages);
    }

    public async Task<AiTestResultDto> TestImageModelAsync(CancellationToken cancellationToken = default)
    {
        var config = await GetAiConfigurationAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(config.ImageBaseUrl) ||
            string.IsNullOrWhiteSpace(config.ImageApiKey) ||
            string.IsNullOrWhiteSpace(config.ImageModel))
        {
            return new AiTestResultDto(
                false,
                config.ImageModel,
                0,
                "请先填写 Base URL、API Key 和 Model");
        }

        var baseUrl = config.ImageBaseUrl.TrimEnd('/');
        var imageUrl = baseUrl.Contains("/image_generation", StringComparison.OrdinalIgnoreCase) ||
                       baseUrl.Contains("/image/generations", StringComparison.OrdinalIgnoreCase) ||
                       baseUrl.Contains("/images/generations", StringComparison.OrdinalIgnoreCase)
            ? baseUrl
            : $"{baseUrl}/images/generations";

        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(120);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", config.ImageApiKey);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var body = new JsonObject
            {
                ["model"] = config.ImageModel,
                ["prompt"] = "a simple red square on white background, minimal design",
                ["n"] = 1,
                ["size"] = "1024x1024",
                ["response_format"] = "url"
            };

            // MiniMax 文生图特殊处理：image_generation 用 aspect_ratio，不接受 OpenAI 的 size/n/response_format
            if (config.ImageProvider.Equals("minimax", StringComparison.OrdinalIgnoreCase))
            {
                body.Remove("size");
                body.Remove("response_format");
                body["aspect_ratio"] = "1:1";
                body["response_format"] = "url";
                body["prompt_optimizer"] = true;
            }

            using var content = new StringContent(
                body.ToJsonString(),
                Encoding.UTF8,
                "application/json");

            using var response = await client.PostAsync(imageUrl, content, cancellationToken);

            stopwatch.Stop();

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                // MiniMax 即使 HTTP 200 也可能在 base_resp.status_code 返回业务错误，需校验
                if (config.ImageProvider.Equals("minimax", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
                        if (doc.RootElement.TryGetProperty("base_resp", out var baseResp) &&
                            baseResp.TryGetProperty("status_code", out var statusCode) &&
                            statusCode.GetInt32() != 0)
                        {
                            var statusMsg = baseResp.TryGetProperty("status_msg", out var msg) ? msg.GetString() : null;
                            return new AiTestResultDto(
                                false,
                                config.ImageModel,
                                (int)stopwatch.ElapsedMilliseconds,
                                $"MiniMax 错误 {statusCode.GetInt32()}：{statusMsg}",
                                responseBody);
                        }
                    }
                    catch
                    {
                        // 解析失败则按成功处理，下方统一返回
                    }
                }

                return new AiTestResultDto(
                    true,
                    config.ImageModel,
                    (int)stopwatch.ElapsedMilliseconds,
                    "文生图模型连接成功");
            }
            else
            {
                return new AiTestResultDto(
                    false,
                    config.ImageModel,
                    (int)stopwatch.ElapsedMilliseconds,
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}",
                    responseBody);
            }
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return new AiTestResultDto(
                false,
                config.ImageModel,
                (int)stopwatch.ElapsedMilliseconds,
                $"连接失败：{FlattenError(exception)}",
                exception.ToString());
        }
    }
}
