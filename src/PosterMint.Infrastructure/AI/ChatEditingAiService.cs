using PosterMint.Application.AI;
using PosterMint.Application.Configs;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PosterMint.Infrastructure.AI;

/// <summary>
/// AI 服务实现（v2 一期最小版）：
/// - GetStatus：告诉 PC 后台"外部大模型是否已配好、当前 provider/model"
/// - TestConnectionAsync：向配置的 chat 端点发一次 minimal 请求，验证 API Key 有效
///
/// v1 的 ApplyAsync（对话式改字段/图层）逻辑已随老会话体系一起废弃；
/// 未来接小程序端 PSP slot patch 时再新增 ApplyPspPatch 方法。
/// </summary>
public sealed class ChatEditingAiService(
    IHttpClientFactory httpClientFactory,
    IConfigService configService,
    IOptions<LlmOptions> optionsAccessor) : IChatEditingAiService
{
    public LlmStatusDto GetStatus()
    {
        var setup = configService.GetAiConfigurationAsync().GetAwaiter().GetResult();
        var options = BuildEffectiveOptions(setup);
        var externalAvailable = options.Enabled &&
                                !string.IsNullOrWhiteSpace(options.ApiKey) &&
                                !string.IsNullOrWhiteSpace(options.Model) &&
                                (!string.IsNullOrWhiteSpace(options.ChatUrl) || !string.IsNullOrWhiteSpace(options.BaseUrl));

        return new LlmStatusDto(
            externalAvailable,
            options.Provider,
            options.Model,
            externalAvailable ? "external" : "local");
    }

    public async Task<AiTestResultDto> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var setup = await configService.GetAiConfigurationAsync(cancellationToken);
        var options = BuildEffectiveOptions(setup);

        if (!options.Enabled ||
            string.IsNullOrWhiteSpace(options.ApiKey) ||
            string.IsNullOrWhiteSpace(options.Model) ||
            (string.IsNullOrWhiteSpace(options.ChatUrl) && string.IsNullOrWhiteSpace(options.BaseUrl)))
        {
            return new AiTestResultDto(
                false,
                options.Model,
                0,
                "请先填写 API Key、Model 和 Base URL");
        }

        var chatUrl = ResolveChatUrl(options);

        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(Math.Max(options.TimeoutSeconds, 10));
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var body = new JsonObject
            {
                ["model"] = options.Model,
                ["messages"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["role"] = "user",
                        ["content"] = "你好，请回复一个字：是"
                    }
                },
                ["max_tokens"] = 5,
                ["temperature"] = 0.1
            };

            using var content = new StringContent(
                body.ToJsonString(),
                Encoding.UTF8,
                "application/json");

            using var response = await client.PostAsync(chatUrl, content, cancellationToken);

            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(responseContent);
                var responseMessage = document.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                return new AiTestResultDto(
                    true,
                    options.Model,
                    (int)stopwatch.ElapsedMilliseconds,
                    $"连接成功，模型回复：{responseMessage}");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return new AiTestResultDto(
                    false,
                    options.Model,
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
                options.Model,
                (int)stopwatch.ElapsedMilliseconds,
                $"连接失败：{exception.Message}",
                exception.ToString());
        }
    }

    private static string ResolveChatUrl(LlmOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ChatUrl))
        {
            return options.ChatUrl;
        }

        var baseUrl = options.BaseUrl.TrimEnd('/');

        // BaseUrl 已是完整 chat 端点（MiniMax 的 .../chatcompletion_v2、
        // OpenAI 的 .../chat/completions）时直接使用，不再追加路径。
        if (baseUrl.Contains("/chatcompletion", StringComparison.OrdinalIgnoreCase) ||
            baseUrl.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return baseUrl;
        }

        return $"{baseUrl}/chat/completions";
    }

    private LlmOptions BuildEffectiveOptions(AiConfigurationDto setup)
    {
        var fallback = optionsAccessor.Value;
        return new LlmOptions
        {
            Enabled = fallback.Enabled,
            Provider = string.IsNullOrWhiteSpace(setup.TextProvider) ? fallback.Provider : setup.TextProvider,
            BaseUrl = string.IsNullOrWhiteSpace(setup.TextBaseUrl) ? fallback.BaseUrl : setup.TextBaseUrl,
            ChatUrl = string.IsNullOrWhiteSpace(setup.TextChatUrl) ? fallback.ChatUrl : setup.TextChatUrl,
            ApiKey = string.IsNullOrWhiteSpace(setup.TextApiKey) ? fallback.ApiKey : setup.TextApiKey,
            Model = string.IsNullOrWhiteSpace(setup.TextModel) ? fallback.Model : setup.TextModel,
            TimeoutSeconds = fallback.TimeoutSeconds,
            Temperature = fallback.Temperature,
            ResponseFormat = fallback.ResponseFormat
        };
    }
}
