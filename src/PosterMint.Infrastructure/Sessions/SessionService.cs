using PosterMint.Application.AI;
using PosterMint.Application.Sessions;
using PosterMint.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace PosterMint.Infrastructure.Sessions;

public sealed class SessionService(
    PosterMintDbContext dbContext,
    IChatEditingAiService chatEditingAiService,
    ISessionInteractionStore interactionStore) : ISessionService
{
    public async Task<PosterSessionDto> BootstrapAsync(
        BootstrapSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var templateKey = request.Scene.Trim().ToLowerInvariant() switch
        {
            "menu" => "menu-grid",
            "store-promo" => "store-promo",
            _ => "single-dish-red"
        };

        var template = await dbContext.Templates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TemplateKey == templateKey, cancellationToken);

        if (template is null)
        {
            throw new InvalidOperationException($"Template '{templateKey}' was not found.");
        }

        return await CreateInternalAsync(new CreateSessionRequest
        {
            TemplateId = template.Id,
            Name = string.IsNullOrWhiteSpace(request.Goal) ? template.Name : request.Goal,
            Scene = request.Scene,
            Goal = request.Goal,
            ReferenceImageDataUrl = request.ReferenceImageDataUrl
        }, cancellationToken);
    }

    public Task<PosterSessionDto> CreateAsync(CreateSessionRequest request, CancellationToken cancellationToken = default) =>
        CreateInternalAsync(request, cancellationToken);

    public async Task<PosterSessionDto?> GetAsync(string sessionKey, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SessionKey == sessionKey, cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<PosterSessionDto> UpdateFieldsAsync(
        string sessionKey,
        UpdateSessionFieldsRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await FindSessionAsync(sessionKey, cancellationToken);
        var snapshot = ParseObject(entity.TemplateSnapshotJson);
        var fieldDefinitions = snapshot["fields"] as JsonArray ?? [];
        var currentFields = ParseObject(entity.CurrentFieldsJson);

        foreach (var field in fieldDefinitions)
        {
            if (field is not JsonObject fieldObject)
            {
                continue;
            }

            var key = fieldObject["key"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(key) || !request.Fields.TryGetValue(key, out var incomingValue))
            {
                continue;
            }

            currentFields[key] = NormalizeFieldValue(incomingValue, fieldObject);
        }

        entity.CurrentFieldsJson = currentFields.ToJsonString();
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var state = interactionStore.GetOrCreate(sessionKey);
        AddVersion(state, "表单微调");
        state.Messages.Add(new SessionMessageDto("assistant", "系统", "我已根据你的手动修改同步更新预览。", DateTimeOffset.UtcNow));

        return Map(entity);
    }

    public async Task<SessionChatResultDto> ApplyChatAsync(
        string sessionKey,
        ApplySessionChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await FindSessionAsync(sessionKey, cancellationToken);
        var snapshot = ParseObject(entity.TemplateSnapshotJson);
        var currentFields = ParseObject(entity.CurrentFieldsJson);
        var currentLayout = ParseArray(entity.CurrentLayoutJson);
        var fieldDefinitions = snapshot["fields"] as JsonArray ?? [];
        var state = interactionStore.GetOrCreate(sessionKey);

        foreach (var asset in request.Assets)
        {
            state.Assets.RemoveAll(x => x.Kind == asset.Kind);
            state.Assets.Add(asset);
        }

        state.Messages.Add(new SessionMessageDto("user", "你", request.Message, DateTimeOffset.UtcNow));

        var result = await chatEditingAiService.ApplyAsync(
            request.Message,
            snapshot,
            currentFields,
            currentLayout,
            state.Messages,
            state.Assets,
            cancellationToken);

        foreach (var pair in result.UpdatedFields)
        {
            var fieldDefinition = fieldDefinitions
                .OfType<JsonObject>()
                .FirstOrDefault(x => string.Equals(x["key"]?.GetValue<string>(), pair.Key, StringComparison.OrdinalIgnoreCase));

            currentFields[pair.Key] = fieldDefinition is null
                ? pair.Value
                : NormalizeFieldValue(pair.Value, fieldDefinition);
        }

        foreach (var patch in result.UpdatedLayoutNodes)
        {
            if (patch.NodeIndex < 0 || patch.NodeIndex >= currentLayout.Count || currentLayout[patch.NodeIndex] is not JsonObject node)
            {
                continue;
            }

            foreach (var change in patch.Changes)
            {
                node[change.Key] = change.Value?.DeepClone();
            }
        }

        entity.CurrentFieldsJson = currentFields.ToJsonString();
        entity.CurrentLayoutJson = currentLayout.ToJsonString();
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var changeSummary = DescribeChanges(result);
        AddVersion(state, changeSummary);
        state.Messages.Add(new SessionMessageDto("assistant", result.Mode == "external" ? "创作助手" : "本地助手", result.Reply, DateTimeOffset.UtcNow));

        var session = Map(entity);
        return new SessionChatResultDto(
            result.Reply,
            result.Mode,
            result.UpdatedFields,
            result.UpdatedLayoutNodes,
            result.Warnings,
            session);
    }

    private async Task<PosterSessionDto> CreateInternalAsync(CreateSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.TemplateId <= 0)
        {
            throw new InvalidOperationException("TemplateId is required.");
        }

        var template = await dbContext.Templates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.TemplateId, cancellationToken);

        if (template is null)
        {
            throw new InvalidOperationException($"Template '{request.TemplateId}' was not found.");
        }

        var fields = ExtractDefaultFields(template.FieldsJson);
        ApplyReferenceImageIfPossible(template.TemplateKey, request.ReferenceImageDataUrl, fields);

        var snapshot = new JsonObject
        {
            ["templateId"] = template.Id,
            ["templateKey"] = template.TemplateKey,
            ["name"] = template.Name,
            ["canvas"] = JsonNode.Parse(template.CanvasJson),
            ["fields"] = JsonNode.Parse(template.FieldsJson),
            ["layout"] = JsonNode.Parse(template.LayoutJson)
        };

        var now = DateTimeOffset.UtcNow;
        var entity = new Domain.Entities.PosterSessionEntity
        {
            SessionKey = $"sess_{Guid.NewGuid():N}"[..18],
            TemplateId = template.Id,
            Name = string.IsNullOrWhiteSpace(request.Name)
                ? $"{template.Name} 会话"
                : request.Name.Trim(),
            TemplateSnapshotJson = snapshot.ToJsonString(),
            CurrentFieldsJson = fields.ToJsonString(),
            CurrentLayoutJson = template.LayoutJson,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Sessions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        var state = interactionStore.GetOrCreate(entity.SessionKey);
        state.Scene = request.Scene;
        state.Goal = request.Goal;
        state.ReferenceImageDataUrl = request.ReferenceImageDataUrl;
        state.Messages.Clear();
        state.Versions.Clear();
        state.Assets.Clear();
        state.SuggestedActions.Clear();

        if (!string.IsNullOrWhiteSpace(request.ReferenceImageDataUrl))
        {
            state.Assets.Add(new SessionAssetDto("referenceImage", "reference-upload", request.ReferenceImageDataUrl));
        }

        state.Messages.Add(new SessionMessageDto(
            "assistant",
            "创作助手",
            BuildWelcomeMessage(template.TemplateKey, request.ReferenceImageDataUrl),
            now));
        AddVersion(state, "初始草稿");
        foreach (var action in BuildSuggestedActions(template.TemplateKey))
        {
            state.SuggestedActions.Add(action);
        }

        return Map(entity);
    }

    private async Task<Domain.Entities.PosterSessionEntity> FindSessionAsync(string sessionKey, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Sessions
            .FirstOrDefaultAsync(x => x.SessionKey == sessionKey, cancellationToken);

        return entity ?? throw new InvalidOperationException($"Session '{sessionKey}' was not found.");
    }

    private PosterSessionDto Map(Domain.Entities.PosterSessionEntity entity)
    {
        var state = interactionStore.GetOrCreate(entity.SessionKey);
        return new PosterSessionDto(
            entity.SessionKey,
            entity.TemplateId,
            entity.Name,
            entity.Status,
            state.Scene,
            state.Goal,
            state.ReferenceImageDataUrl,
            JsonNode.Parse(entity.TemplateSnapshotJson),
            JsonNode.Parse(entity.CurrentFieldsJson),
            JsonNode.Parse(entity.CurrentLayoutJson),
            state.Messages.ToList(),
            state.Versions.ToList(),
            state.SuggestedActions.ToList(),
            state.Assets.ToList(),
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private static JsonObject ExtractDefaultFields(string fieldsJson)
    {
        var result = new JsonObject();
        var node = JsonNode.Parse(fieldsJson) as JsonArray ?? [];

        foreach (var item in node)
        {
            if (item is not JsonObject fieldObject)
            {
                continue;
            }

            var key = fieldObject["key"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (fieldObject["default"] is not null)
            {
                result[key] = fieldObject["default"]!.DeepClone();
                continue;
            }

            var type = fieldObject["type"]?.GetValue<string>() ?? "text";
            result[key] = type switch
            {
                "features" => new JsonArray(),
                "items" => new JsonArray(),
                _ => string.Empty
            };
        }

        return result;
    }

    private static void ApplyReferenceImageIfPossible(string templateKey, string? referenceImageDataUrl, JsonObject fields)
    {
        if (string.IsNullOrWhiteSpace(referenceImageDataUrl))
        {
            return;
        }

        if (templateKey == "store-promo")
        {
            fields["storeImage"] = referenceImageDataUrl;
            return;
        }

        fields["dishImage"] = referenceImageDataUrl;
    }

    private static string BuildWelcomeMessage(string templateKey, string? referenceImageDataUrl)
    {
        var intro = !string.IsNullOrWhiteSpace(referenceImageDataUrl)
            ? "我已经接收参考图，并先生成了一版可编辑初稿。"
            : "我已经为你打开一个可编辑初稿。";

        var hints = templateKey switch
        {
            "menu-grid" => "你可以直接说“把标题改成招牌菜单”“所有价格都便宜一点”“把第一个菜换成剁椒鱼头”。",
            "store-promo" => "你可以直接说“店名改成老街川味馆”“宣传标题更热闹一点”“把地址换成南京西路 88 号”。",
            _ => "你可以直接说“店名改成海底捞”“活动价改成 59.9”“把菜名加粗一点”“把二维码移到右下角”。"
        };

        return $"{intro} 直接告诉我你想怎么改，我会一边改内容，一边更新预览。{hints}";
    }

    private static IReadOnlyList<SuggestedActionDto> BuildSuggestedActions(string templateKey) =>
        templateKey switch
        {
            "menu-grid" =>
            [
                new SuggestedActionDto("改标题", "把标题改成招牌菜单"),
                new SuggestedActionDto("降一点价", "所有价格都便宜一点"),
                new SuggestedActionDto("换配色", "整体配色更清爽一点")
            ],
            "store-promo" =>
            [
                new SuggestedActionDto("改店名", "店名改成老街川味馆"),
                new SuggestedActionDto("改简介", "宣传标题改成地道川味，现炒现做"),
                new SuggestedActionDto("换地址", "地址改成南京西路 88 号")
            ],
            _ =>
            [
                new SuggestedActionDto("改店名", "店名改成老街川味馆"),
                new SuggestedActionDto("改活动价", "活动价改成59.9"),
                new SuggestedActionDto("加粗菜名", "把菜名加粗一点")
            ]
        };

    private static string DescribeChanges(AiEditResultDto result)
    {
        var fieldNames = result.UpdatedFields.Keys.ToList();
        if (fieldNames.Count > 0)
        {
            return $"改 {string.Join("、", fieldNames)}";
        }

        if (result.UpdatedLayoutNodes.Count > 0)
        {
            return "调布局";
        }

        return "对话修改";
    }

    private static void AddVersion(SessionInteractionState state, string description)
    {
        var next = state.Versions.Count + 1;
        state.Versions.Add(new SessionVersionDto(next, description, DateTimeOffset.UtcNow));
        if (state.Versions.Count > 20)
        {
            state.Versions.RemoveAt(0);
        }
    }

    private static JsonNode NormalizeFieldValue(string? incomingValue, JsonObject fieldObject)
    {
        var type = fieldObject["type"]?.GetValue<string>() ?? "text";
        var raw = incomingValue?.Trim() ?? string.Empty;

        return type switch
        {
            "price" => raw.TrimStart('￥', '¥'),
            "features" => new JsonArray(
                raw.Split(['，', ',', '、', '\n', ';', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(value => JsonValue.Create(value))
                    .ToArray()),
            "items" => BuildItems(raw),
            _ => TrimWithMaxLength(raw, fieldObject["maxLength"]?.GetValue<int?>())
        };
    }

    private static JsonArray BuildItems(string raw)
    {
        var items = new JsonArray();
        foreach (var line in raw.Split(['\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            items.Add(new JsonObject
            {
                ["name"] = parts.ElementAtOrDefault(0) ?? string.Empty,
                ["price"] = (parts.ElementAtOrDefault(1) ?? string.Empty).TrimStart('￥', '¥'),
                ["image"] = parts.ElementAtOrDefault(2) ?? string.Empty
            });
        }

        return items;
    }

    private static string TrimWithMaxLength(string raw, int? maxLength)
    {
        if (!maxLength.HasValue || raw.Length <= maxLength.Value)
        {
            return raw;
        }

        return raw[..maxLength.Value];
    }

    private static JsonObject ParseObject(string json) => JsonNode.Parse(json) as JsonObject ?? [];

    private static JsonArray ParseArray(string json) => JsonNode.Parse(json) as JsonArray ?? [];
}
