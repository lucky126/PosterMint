namespace PosterMint.Application.AI;

/// <summary>
/// 支持的 LLM Provider 常量和默认配置
/// </summary>
public static class LlmProviders
{
    public const string OpenAiCompatible = "openai-compatible";
    public const string Wenxin = "wenxin";
    public const string MiniMax = "minimax";
    public const string Glm = "glm";
    public const string Kimi = "kimi";
    public const string DeepSeek = "deepseek";
    public const string Qwen = "qwen";
    public const string Doubao = "doubao";
    public const string SenseNova = "sensenova";
    public const string Spark = "spark";

    /// <summary>
    /// 所有支持的文本模型提供商
    /// </summary>
    public static IReadOnlyList<LlmProviderOption> AllProviders { get; } =
    [
        new LlmProviderOption(OpenAiCompatible, "OpenAI 兼容", "https://api.openai.com/v1", "gpt-4o-mini"),
        new LlmProviderOption(Qwen, "通义千问", "https://dashscope.aliyuncs.com/compatible-mode/v1", "qwen-max"),
        new LlmProviderOption(Doubao, "豆包", "https://ark.cn-beijing.volces.com/api/v3", "ep-20250629"),
        new LlmProviderOption(Kimi, "Kimi", "https://api.moonshot.cn/v1", "moonshot-v1-8k"),
        new LlmProviderOption(Glm, "智谱清言", "https://open.bigmodel.cn/api/paas/v4", "glm-4"),
        new LlmProviderOption(DeepSeek, "DeepSeek", "https://api.deepseek.com/v1", "deepseek-chat"),
        new LlmProviderOption(MiniMax, "MiniMax", "https://api.minimax.chat/v1/text/chatcompletion_v2", "MiniMax-Text-01"),
        new LlmProviderOption(Wenxin, "文心一言", "https://qianfan.baidubce.com/v2", "ernie-4.0"),
        new LlmProviderOption(SenseNova, "商汤日日新", "https://api.sensenova.cn/v1", "SenseChat-5"),
        new LlmProviderOption(Spark, "讯飞星火", "https://spark-api-open.xf-yun.com/v1", "generalv3.5")
    ];

    /// <summary>
    /// 所有支持的文生图模型提供商
    /// </summary>
    public static IReadOnlyList<LlmProviderOption> ImageProviders { get; } =
    [
        new LlmProviderOption(OpenAiCompatible, "OpenAI DALL-E", "https://api.openai.com/v1", "dall-e-3"),
        new LlmProviderOption(Qwen, "通义万相", "https://dashscope.aliyuncs.com/compatible-mode/v1", "wanx-v1"),
        new LlmProviderOption(Doubao, "豆包图像生成", "https://ark.cn-beijing.volces.com/api/v3", "image-v1"),
        new LlmProviderOption(Glm, "智谱CogView", "https://open.bigmodel.cn/api/paas/v4", "cogview-3"),
        new LlmProviderOption(MiniMax, "MiniMax", "https://api.minimaxi.com/v1/image_generation", "image-01"),
        new LlmProviderOption(Wenxin, "文心一格", "https://qianfan.baidubce.com/v2", "image/v1/image/generate"),
        new LlmProviderOption(SenseNova, "商汤灵境", "https://api.sensenova.cn/v1", "sensenova-v2.5")
    ];
}

/// <summary>
/// LLM Provider 配置选项
/// </summary>
public sealed record LlmProviderOption(
    string Value,
    string Label,
    string DefaultBaseUrl,
    string DefaultModel);
