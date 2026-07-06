using PosterMint.Application.AI;
using PosterMint.Application.Configs;
using PosterMint.Application.Sessions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PosterMint.Infrastructure.AI;

public sealed class ChatEditingAiService(
    IHttpClientFactory httpClientFactory,
    IConfigService configService,
    IOptions<LlmOptions> optionsAccessor,
    ILogger<ChatEditingAiService> logger) : IChatEditingAiService
{
    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dishName"] = ["dishName", "菜品名称", "菜名", "品名", "主菜"],
        ["price"] = ["price", "原价", "门市价"],
        ["promoPrice"] = ["promoPrice", "促销价", "活动价", "特价", "优惠价"],
        ["slogan"] = ["slogan", "促销语", "活动文案", "副标题"],
        ["storeName"] = ["storeName", "店铺名称", "店名", "门店名称", "商家名称"],
        ["title"] = ["title", "菜单标题", "标题"],
        ["subtitle"] = ["subtitle", "副标题", "说明"],
        ["headline"] = ["headline", "宣传标题", "主标题", "宣传语"],
        ["description"] = ["description", "简介", "店铺简介", "介绍"],
        ["address"] = ["address", "地址", "门店地址"],
        ["phone"] = ["phone", "电话", "联系电话", "手机号"],
        ["features"] = ["features", "特色", "卖点", "标签"],
        ["items"] = ["items", "菜品列表", "菜单"],
        ["qrcode"] = ["qrcode", "二维码", "qr"],
        ["dishImage"] = ["dishImage", "菜品图", "菜品图片", "实拍图"],
        ["storeImage"] = ["storeImage", "店铺图", "店铺图片", "门头图", "店铺照片"]
    };

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

    public async Task<AiEditResultDto> ApplyAsync(
        string message,
        JsonObject templateSnapshot,
        JsonObject currentFields,
        JsonArray currentLayout,
        IReadOnlyList<SessionMessageDto> conversationHistory,
        IReadOnlyList<SessionAssetDto> assets,
        CancellationToken cancellationToken = default)
    {
        var setup = await configService.GetAiConfigurationAsync(cancellationToken);
        var options = BuildEffectiveOptions(setup);
        var status = new LlmStatusDto(
            options.Enabled &&
            !string.IsNullOrWhiteSpace(options.ApiKey) &&
            !string.IsNullOrWhiteSpace(options.Model) &&
            (!string.IsNullOrWhiteSpace(options.ChatUrl) || !string.IsNullOrWhiteSpace(options.BaseUrl)),
            options.Provider,
            options.Model,
            (!string.IsNullOrWhiteSpace(options.ApiKey) && !string.IsNullOrWhiteSpace(options.Model)) ? "external" : "local");

        // 本地规则先执行：图片替换（ApplyAssetRules）、快捷命令识别等是纯本地逻辑，与模型无关，必须始终执行
        var localResult = ApplyLocal(message, templateSnapshot, currentFields, currentLayout, conversationHistory, assets);

        if (status.ExternalAvailable)
        {
            try
            {
                var externalResult = await ApplyExternalAsync(
                    message,
                    templateSnapshot,
                    currentFields,
                    currentLayout,
                    conversationHistory,
                    assets,
                    options,
                    cancellationToken);

                // 合并：本地图片替换等必须始终执行（ApplyAssetRules），外部模型的字段修改优先级更高
                var mergedFields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var field in localResult.UpdatedFields)
                {
                    mergedFields[field.Key] = field.Value;
                }
                foreach (var field in externalResult.UpdatedFields)
                {
                    mergedFields[field.Key] = field.Value;
                }

                if (mergedFields.Count > 0 || externalResult.UpdatedLayoutNodes.Count > 0)
                {
                    return new AiEditResultDto(
                        externalResult.Reply,
                        externalResult.Mode,
                        mergedFields,
                        externalResult.UpdatedLayoutNodes,
                        localResult.Warnings.Concat(externalResult.Warnings).Distinct().ToList());
                }
                return localResult;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "External LLM editing failed. Falling back to local editor.");
                return localResult with
                {
                    Mode = "fallback",
                    Reply = $"⚠️ 外部模型调用失败，已用本地规则兜底：{exception.Message}",
                    Warnings = localResult.Warnings.Concat([$"外部模型调用失败：{exception.Message}"]).ToList()
                };
            }
        }

        return localResult;
    }

    private async Task<AiEditResultDto> ApplyExternalAsync(
        string message,
        JsonObject templateSnapshot,
        JsonObject currentFields,
        JsonArray currentLayout,
        IReadOnlyList<SessionMessageDto> conversationHistory,
        IReadOnlyList<SessionAssetDto> assets,
        LlmOptions options,
        CancellationToken cancellationToken)
    {
        var chatUrl = ResolveChatUrl(options);

        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        var body = new JsonObject
        {
            ["model"] = options.Model,
            // 部分模型（如 MiniMax）要求 temperature ∈ (0,1]，0 或过大都会被拒
            ["temperature"] = Math.Clamp(options.Temperature <= 0 ? 0.7 : options.Temperature, 0.01, 1.0),
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "system",
                    ["content"] = BuildSystemPrompt()
                },
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = BuildUserPrompt(message, templateSnapshot, currentFields, currentLayout, conversationHistory, assets)
                }
            }
        };

        // response_format:json_object 是 OpenAI 特有参数，MiniMax(chatcompletion_v2) 不支持（错误 2013）。
        // 仅对声明支持的 provider 发送；MiniMax 依赖系统提示要求返回 JSON + 文本兜底解析。
        if (options.ResponseFormat &&
            !options.Provider.Equals("minimax", StringComparison.OrdinalIgnoreCase))
        {
            body["response_format"] = new JsonObject
            {
                ["type"] = "json_object"
            };
        }

        using var response = await client.PostAsync(
            chatUrl,
            new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
            cancellationToken);

        var payloadText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"HTTP {(int)response.StatusCode}：{Truncate(payloadText, 400)}");
        }

        using var document = JsonDocument.Parse(payloadText);
        var root = document.RootElement;

        // MiniMax 等即使 HTTP 200，也可能在 base_resp 里返回业务错误
        if (root.TryGetProperty("base_resp", out var baseResp) &&
            baseResp.TryGetProperty("status_code", out var statusCode) &&
            statusCode.TryGetInt32(out var code) && code != 0)
        {
            var statusMsg = baseResp.TryGetProperty("status_msg", out var m) ? m.GetString() : null;
            throw new InvalidOperationException($"模型返回错误 {code}：{statusMsg}");
        }

        if (!root.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException(
                $"模型响应缺少 choices：{Truncate(payloadText, 400)}");
        }

        var content = choices[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "{}";

        var json = ParseJsonObject(content);
        return new AiEditResultDto(
            json["reply"]?.GetValue<string>() ?? "已根据你的要求更新海报内容。",
            "external",
            ParseStringDictionary(json["updatedFields"] as JsonObject),
            ParseLayoutPatches(json["updatedLayoutNodes"] as JsonArray),
            ParseWarnings(json["warnings"] as JsonArray));
    }

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max] + "…";

    private static AiEditResultDto ApplyLocal(
        string message,
        JsonObject templateSnapshot,
        JsonObject currentFields,
        JsonArray currentLayout,
        IReadOnlyList<SessionMessageDto> conversationHistory,
        IReadOnlyList<SessionAssetDto> assets)
    {
        var fieldDefinitions = templateSnapshot["fields"] as JsonArray ?? [];
        var updatedFields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var patches = new List<LayoutNodePatchDto>();
        var warnings = new List<string>();
        var touched = new List<string>();

        ApplyCommonFieldRules(message, fieldDefinitions, currentFields, updatedFields, touched);
        ApplyAssetRules(message, fieldDefinitions, assets, updatedFields, touched);
        ApplyFollowUpRules(message, currentFields, conversationHistory, updatedFields, touched);

        foreach (var fieldNode in fieldDefinitions)
        {
            if (fieldNode is not JsonObject fieldObject)
            {
                continue;
            }

            var key = fieldObject["key"]?.GetValue<string>();
            var type = fieldObject["type"]?.GetValue<string>() ?? "text";
            if (string.IsNullOrWhiteSpace(key) || updatedFields.ContainsKey(key))
            {
                continue;
            }

            var extracted = TryExtractFieldValue(message, key, type);
            if (string.IsNullOrWhiteSpace(extracted))
            {
                continue;
            }

            updatedFields[key] = NormalizeFieldValue(type, extracted, fieldObject);
            touched.Add(fieldObject["label"]?.GetValue<string>() ?? key);
        }

        foreach (var fieldNode in fieldDefinitions)
        {
            if (fieldNode is not JsonObject fieldObject)
            {
                continue;
            }

            var key = fieldObject["key"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var patch = TryCreateLayoutPatch(message, key, currentLayout);
            if (patch is not null)
            {
                patches.Add(patch);
                var touchName = $"{fieldObject["label"]?.GetValue<string>() ?? key}样式";
                if (!touched.Contains(touchName, StringComparer.OrdinalIgnoreCase))
                {
                    touched.Add(touchName);
                }
            }
        }

        if (updatedFields.Count == 0 && patches.Count == 0)
        {
            warnings.Add("我还没听懂这次修改。可以直接说“店名改成… / 活动价改成… / 把二维码移到右下角 / 把菜品图换成刚上传的这张”。");
        }

        var reply = touched.Count == 0
            ? "我暂时没有识别到可执行的修改，请再明确一点。"
            : $"已更新：{string.Join("、", touched.Distinct())}。";

        return new AiEditResultDto(reply, "local", updatedFields, patches, warnings);
    }

    private static string BuildSystemPrompt() =>
        """
        你是餐饮海报内容编辑助手。
        你只通过对话帮助用户修改海报内容、样式和局部布局。
        你必须返回 JSON，不要返回 markdown。
        输出结构：
        {
          "reply": "简短中文回复",
          "updatedFields": { "fieldKey": "newValue" },
          "updatedLayoutNodes": [
            { "nodeIndex": 3, "changes": { "fontSize": 68, "fontWeight": 800 } }
          ],
          "warnings": []
        }
        规则：
        1. 只修改模板里已有字段或布局节点。
        2. 如果用户要求生成文案，可以返回建议，但不要编造图片 URL、二维码链接和电话号码。
        3. 要利用对话历史理解“再便宜点”“还是改回198吧”“换成刚上传的这张”这类上下文。
        4. 如果信息不明确，返回 warning，而不是乱改。
        """;

    private static string BuildUserPrompt(
        string message,
        JsonObject templateSnapshot,
        JsonObject currentFields,
        JsonArray currentLayout,
        IReadOnlyList<SessionMessageDto> conversationHistory,
        IReadOnlyList<SessionAssetDto> assets)
    {
        var payload = new JsonObject
        {
            ["message"] = message,
            ["template"] = templateSnapshot.DeepClone(),
            ["currentFields"] = currentFields.DeepClone(),
            ["currentLayout"] = currentLayout.DeepClone(),
            ["conversationHistory"] = new JsonArray(conversationHistory.Select(item => new JsonObject
            {
                ["role"] = item.Role,
                ["text"] = item.Text
            }).ToArray()),
            ["assets"] = new JsonArray(assets.Select(item => new JsonObject
            {
                ["kind"] = item.Kind,
                ["name"] = item.Name
                // 注意：故意不发送 dataUrl（base64 图片数据）给文本模型。
                // 一张图的 base64 可达数 MB，会撑爆请求体导致超时，且文本模型无需图片内容。
                // 图片替换由本地 ApplyAssetRules 直接把 dataUrl 写入字段完成。
            }).ToArray())
        };

        return payload.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static void ApplyCommonFieldRules(
        string message,
        JsonArray fieldDefinitions,
        JsonObject currentFields,
        Dictionary<string, string?> updatedFields,
        List<string> touched)
    {
        TryApplyExplicitValue(message, fieldDefinitions, updatedFields, touched, "storeName", ["把店名改成", "店名改成", "把店铺名称改成", "店铺名称改成", "门店名称改成", "storeName:"]);
        TryApplyExplicitValue(message, fieldDefinitions, updatedFields, touched, "promoPrice", ["把活动价改成", "活动价改成", "把促销价改成", "促销价改成", "特价改成", "promoPrice:"]);
        TryApplyExplicitValue(message, fieldDefinitions, updatedFields, touched, "price", ["把原价改成", "原价改成", "门市价改成", "price:"]);
        TryApplyExplicitValue(message, fieldDefinitions, updatedFields, touched, "dishName", ["把菜名改成", "菜名改成", "把菜品名称改成", "菜品名称改成", "dishName:"]);
        TryApplyExplicitValue(message, fieldDefinitions, updatedFields, touched, "headline", ["把宣传标题改成", "宣传标题改成", "把主标题改成", "主标题改成", "headline:"]);
        TryApplyExplicitValue(message, fieldDefinitions, updatedFields, touched, "slogan", ["把slogan改成", "slogan改成", "把促销语改成", "促销语改成", "slogan:"]);
        TryApplyExplicitValue(message, fieldDefinitions, updatedFields, touched, "address", ["把地址改成", "地址改成", "门店地址改成", "address:"]);
        TryApplyExplicitValue(message, fieldDefinitions, updatedFields, touched, "phone", ["把电话改成", "电话改成", "联系电话改成", "手机号改成", "phone:"]);

        if (!updatedFields.ContainsKey("promoPrice") && message.Contains("便宜", StringComparison.OrdinalIgnoreCase))
        {
            var currentValue = currentFields["promoPrice"]?.GetValue<string>();
            if (decimal.TryParse(currentValue, out var numeric))
            {
                updatedFields["promoPrice"] = Math.Max(numeric - 10, 0).ToString("0.##");
                touched.Add("促销价");
            }
        }
    }

    private static void ApplyAssetRules(
        string message,
        JsonArray fieldDefinitions,
        IReadOnlyList<SessionAssetDto> assets,
        Dictionary<string, string?> updatedFields,
        List<string> touched)
    {
        if (assets.Count == 0)
        {
            return;
        }

        var latestImage = assets.LastOrDefault(x => x.Kind is "dishImage" or "storeImage" or "referenceImage");
        var latestQr = assets.LastOrDefault(x => x.Kind == "qrcode");

        // 只要本次带了图片素材，且用户意图与图片相关（或未特别指定），就替换到图片字段。
        // 关键词放宽：替换/换/图片/照片/背景/这张/刚上传 等都算；没图片字段则给出明确提示。
        var imageIntent = latestImage is not null &&
            (message.Contains("图", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("照片", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("背景", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("替换", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("换成", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("换个", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("这张", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("刚上传", StringComparison.OrdinalIgnoreCase));

        if (imageIntent)
        {
            var imageField = fieldDefinitions
                .OfType<JsonObject>()
                .FirstOrDefault(x => string.Equals(x["type"]?.GetValue<string>(), "image", StringComparison.OrdinalIgnoreCase));
            var key = imageField?["key"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(key))
            {
                updatedFields[key] = latestImage!.DataUrl;
                touched.Add(imageField?["label"]?.GetValue<string>() ?? key);
            }
        }

        if (latestQr is not null &&
            (message.Contains("二维码", StringComparison.OrdinalIgnoreCase) || message.Contains("qr", StringComparison.OrdinalIgnoreCase)))
        {
            var qrField = fieldDefinitions
                .OfType<JsonObject>()
                .FirstOrDefault(x => string.Equals(x["type"]?.GetValue<string>(), "qrcode", StringComparison.OrdinalIgnoreCase));
            var key = qrField?["key"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(key))
            {
                updatedFields[key] = latestQr.DataUrl;
                touched.Add(qrField?["label"]?.GetValue<string>() ?? key);
            }
        }
    }

    private static void ApplyFollowUpRules(
        string message,
        JsonObject currentFields,
        IReadOnlyList<SessionMessageDto> conversationHistory,
        Dictionary<string, string?> updatedFields,
        List<string> touched)
    {
        if (!updatedFields.ContainsKey("promoPrice") && message.Contains("再便宜点", StringComparison.OrdinalIgnoreCase))
        {
            var currentValue = currentFields["promoPrice"]?.GetValue<string>();
            if (decimal.TryParse(currentValue, out var numeric))
            {
                updatedFields["promoPrice"] = Math.Max(numeric - 5, 0).ToString("0.##");
                touched.Add("促销价");
            }
        }

        if (!updatedFields.ContainsKey("promoPrice") && message.Contains("改回", StringComparison.OrdinalIgnoreCase))
        {
            var reverted = ExtractAfterPhrase(message, "改回");
            if (!string.IsNullOrWhiteSpace(reverted))
            {
                updatedFields["promoPrice"] = reverted.TrimStart('￥', '¥');
                touched.Add("促销价");
            }
        }

        if (!updatedFields.ContainsKey("qrcode") && message.Contains("二维码呢", StringComparison.OrdinalIgnoreCase))
        {
            var previousQrMention = conversationHistory.LastOrDefault(x => x.Text.Contains("二维码", StringComparison.OrdinalIgnoreCase));
            if (previousQrMention is null)
            {
                return;
            }
        }
    }

    private static void TryApplyExplicitValue(
        string message,
        JsonArray fieldDefinitions,
        Dictionary<string, string?> updatedFields,
        List<string> touched,
        string key,
        string[] phrases)
    {
        if (updatedFields.ContainsKey(key))
        {
            return;
        }

        var fieldDefinition = fieldDefinitions
            .OfType<JsonObject>()
            .FirstOrDefault(x => string.Equals(x["key"]?.GetValue<string>(), key, StringComparison.OrdinalIgnoreCase));
        if (fieldDefinition is null)
        {
            return;
        }

        foreach (var phrase in phrases)
        {
            var value = ExtractAfterPhrase(message, phrase);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            updatedFields[key] = NormalizeFieldValue(fieldDefinition["type"]?.GetValue<string>() ?? "text", value, fieldDefinition);
            touched.Add(fieldDefinition["label"]?.GetValue<string>() ?? key);
            return;
        }
    }

    private static string? TryExtractFieldValue(string message, string key, string type)
    {
        foreach (var alias in Aliases.GetValueOrDefault(key, [key]))
        {
            var extracted = ExtractByAlias(message, alias);
            if (!string.IsNullOrWhiteSpace(extracted))
            {
                return extracted;
            }
        }

        if (type == "price")
        {
            var genericPrice = Regex.Match(message, @"(?:价格|售价|特价)\D*([0-9]+(?:\.[0-9]+)?)", RegexOptions.IgnoreCase);
            if (genericPrice.Success)
            {
                return genericPrice.Groups[1].Value;
            }
        }

        return null;
    }

    private static string? ExtractAfterPhrase(string message, string phrase)
    {
        var index = message.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var raw = message[(index + phrase.Length)..].Trim().Trim('“', '”', '"');
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var cutoff = raw.IndexOfAny(['，', '。', '；', '\n']);
        return cutoff >= 0 ? raw[..cutoff].Trim() : raw.Trim();
    }

    private static string? ExtractByAlias(string message, string alias)
    {
        var aliasIndex = message.IndexOf(alias, StringComparison.OrdinalIgnoreCase);
        if (aliasIndex < 0)
        {
            return null;
        }

        var tail = message[aliasIndex..];
        var verbs = new[] { "改成", "改为", "换成", "设为", "写成", "调整为", "变成", "：", ":" };

        foreach (var verb in verbs)
        {
            var verbIndex = tail.IndexOf(verb, StringComparison.OrdinalIgnoreCase);
            if (verbIndex < 0)
            {
                continue;
            }

            var raw = tail[(verbIndex + verb.Length)..].Trim().Trim('“', '”', '"');
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var cutoff = raw.IndexOfAny(['，', '。', '；', '\n']);
            return cutoff >= 0 ? raw[..cutoff].Trim() : raw.Trim();
        }

        return null;
    }

    private static JsonObject ParseJsonObject(string text)
    {
        var raw = text.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(raw) as JsonObject ?? [];
        }
        catch
        {
            var fenced = Regex.Match(raw, "```(?:json)?\\s*([\\s\\S]*?)```", RegexOptions.IgnoreCase);
            if (fenced.Success)
            {
                return JsonNode.Parse(fenced.Groups[1].Value) as JsonObject ?? [];
            }

            var start = raw.IndexOf('{');
            var end = raw.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                return JsonNode.Parse(raw[start..(end + 1)]) as JsonObject ?? [];
            }

            throw;
        }
    }

    private static Dictionary<string, string?> ParseStringDictionary(JsonObject? jsonObject)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (jsonObject is null)
        {
            return result;
        }

        foreach (var item in jsonObject)
        {
            // 注意：不要用 ToJsonString()——它会把非 ASCII 中文转成 \uXXXX 编码序列，导致预览乱码。
            // 直接用 GetValue<string>() 取原始 Unicode 字符串。
            result[item.Key] = item.Value?.GetValue<string>();
        }

        return result;
    }

    private static List<LayoutNodePatchDto> ParseLayoutPatches(JsonArray? jsonArray)
    {
        var result = new List<LayoutNodePatchDto>();
        if (jsonArray is null)
        {
            return result;
        }

        foreach (var item in jsonArray)
        {
            if (item is not JsonObject patchObject)
            {
                continue;
            }

            var nodeIndex = patchObject["nodeIndex"]?.GetValue<int?>() ?? -1;
            var changes = patchObject["changes"] as JsonObject;
            if (nodeIndex < 0 || changes is null)
            {
                continue;
            }

            result.Add(new LayoutNodePatchDto(nodeIndex, changes));
        }

        return result;
    }

    private static List<string> ParseWarnings(JsonArray? jsonArray) =>
        jsonArray?.Select(x => x?.GetValue<string>() ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList()
        ?? [];

    private static string NormalizeFieldValue(string type, string value, JsonObject fieldDefinition)
    {
        if (type.Equals("price", StringComparison.OrdinalIgnoreCase))
        {
            return Regex.Replace(value.Trim(), @"^[￥¥]", string.Empty);
        }

        if (type.Equals("features", StringComparison.OrdinalIgnoreCase))
        {
            var tags = value.Split(['，', ',', '、', ';', '；', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return string.Join("、", tags.Take(3));
        }

        if (type.Equals("items", StringComparison.OrdinalIgnoreCase))
        {
            return value.Trim();
        }

        var maxLength = fieldDefinition["maxLength"]?.GetValue<int?>();
        var text = value.Trim();
        if (maxLength.HasValue && text.Length > maxLength.Value)
        {
            text = text[..maxLength.Value];
        }

        return text;
    }

    private static LayoutNodePatchDto? TryCreateLayoutPatch(string message, string key, JsonArray layout)
    {
        var aliasValues = Aliases.GetValueOrDefault(key, [key]);
        var hitAlias = aliasValues.Any(alias => message.Contains(alias, StringComparison.OrdinalIgnoreCase));
        if (!hitAlias)
        {
            return null;
        }

        for (var i = 0; i < layout.Count; i++)
        {
            if (layout[i] is not JsonObject node)
            {
                continue;
            }

            if (!string.Equals(node["field"]?.GetValue<string>(), key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var changes = new JsonObject();

            if (message.Contains("加粗", StringComparison.OrdinalIgnoreCase))
            {
                var weight = node["fontWeight"]?.GetValue<int?>() ?? 600;
                changes["fontWeight"] = Math.Min(weight + 200, 900);
            }

            if (message.Contains("大一点", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("放大", StringComparison.OrdinalIgnoreCase))
            {
                var fontSize = node["fontSize"]?.GetValue<int?>() ?? 32;
                changes["fontSize"] = fontSize + 6;
            }

            if (message.Contains("小一点", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("缩小", StringComparison.OrdinalIgnoreCase))
            {
                var fontSize = node["fontSize"]?.GetValue<int?>() ?? 32;
                changes["fontSize"] = Math.Max(fontSize - 6, 16);
            }

            if (message.Contains("居中", StringComparison.OrdinalIgnoreCase))
            {
                changes["align"] = "center";
            }

            if (message.Contains("左上角", StringComparison.OrdinalIgnoreCase))
            {
                changes["x"] = 80;
                changes["y"] = 80;
            }

            if (message.Contains("右下角", StringComparison.OrdinalIgnoreCase))
            {
                changes["x"] = 760;
                changes["y"] = 1520;
            }

            return changes.Count == 0 ? null : new LayoutNodePatchDto(i, changes);
        }

        return null;
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
