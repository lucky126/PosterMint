using System.Text.Json.Nodes;

namespace PosterMint.Application.PspTemplates;

/// <summary>
/// PSP-v1 校验器（对应 docs/PSP-v1.md §5 规则）。
/// 服务端权威、纯函数、无副作用。前端只做提示，最终以这里为准。
/// </summary>
public static class PspValidator
{
    public const string SupportedSchema = "PSP-v1";

    public static PspValidationResult Validate(JsonNode? root)
    {
        var errors = new List<string>();

        if (root is not JsonObject psp)
        {
            errors.Add("PSP 顶层必须是 JSON 对象。");
            return new PspValidationResult(false, string.Empty, 0, errors);
        }

        // §5.1 顶层必填 5 字段
        var schema = psp["schema"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrEmpty(schema))
        {
            errors.Add("缺少字段：schema。");
        }
        else if (!string.Equals(schema, SupportedSchema, StringComparison.Ordinal))
        {
            errors.Add($"仅支持 schema=\"{SupportedSchema}\"，收到 \"{schema}\"。");
        }

        if (string.IsNullOrWhiteSpace(psp["id"]?.GetValue<string>()))
        {
            errors.Add("缺少字段：id。");
        }
        if (string.IsNullOrWhiteSpace(psp["name"]?.GetValue<string>()))
        {
            errors.Add("缺少字段：name。");
        }

        var canvas = psp["canvas"] as JsonObject;
        var canvasW = 0;
        var canvasH = 0;
        if (canvas is null)
        {
            errors.Add("缺少字段：canvas。");
        }
        else
        {
            canvasW = TryInt(canvas["w"]);
            canvasH = TryInt(canvas["h"]);
            if (canvasW <= 0) errors.Add("canvas.w 必须是正整数。");
            if (canvasH <= 0) errors.Add("canvas.h 必须是正整数。");
        }

        var layers = psp["layers"] as JsonArray;
        if (layers is null)
        {
            errors.Add("缺少字段：layers。");
            // 缺 layers 就没法继续检查内部
            return new PspValidationResult(errors.Count == 0, schema ?? string.Empty, 0, errors);
        }

        // 全局唯一性集合
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var slotCount = 0;

        WalkLayers(layers, canvasW, canvasH, "layers", seenIds, ref slotCount, errors);

        return new PspValidationResult(
            IsValid: errors.Count == 0,
            SchemaVersion: schema ?? string.Empty,
            SlotCount: slotCount,
            Errors: errors);
    }

    private static void WalkLayers(
        JsonArray layers,
        int canvasW,
        int canvasH,
        string path,
        HashSet<string> seenIds,
        ref int slotCount,
        List<string> errors)
    {
        // §5 同层 path 唯一
        var seenPathsInThisLevel = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < layers.Count; i++)
        {
            var here = $"{path}[{i}]";
            if (layers[i] is not JsonObject layer)
            {
                errors.Add($"{here} 必须是对象。");
                continue;
            }

            var type = layer["type"]?.GetValue<string>();
            switch (type)
            {
                case "background":
                    // §2.1 允许 fill 或 image 二选一，不做强校验
                    break;

                case "text":
                case "image":
                    ValidateBbox(layer, canvasW, canvasH, here, errors);
                    ValidateSlot(layer, here, seenIds, seenPathsInThisLevel, ref slotCount, errors);
                    break;

                case "group":
                    ValidateBbox(layer, canvasW, canvasH, here, errors);
                    RegisterPath(layer, here, seenPathsInThisLevel, errors);

                    var itemTemplate = layer["itemTemplate"] as JsonObject;
                    var subLayers = itemTemplate?["layers"] as JsonArray;
                    if (subLayers is null)
                    {
                        errors.Add($"{here} group 必须包含 itemTemplate.layers 数组。");
                    }
                    else
                    {
                        // 嵌套 group：group.bbox 是相对画布的外框，内部 layer 的 bbox 相对 group（不再对 canvas 做越界检查）
                        WalkLayers(subLayers, int.MaxValue, int.MaxValue, $"{here}.itemTemplate.layers", seenIds, ref slotCount, errors);
                    }
                    break;

                case null:
                    errors.Add($"{here} 缺少 type 字段。");
                    break;

                default:
                    errors.Add($"{here} 未知 layer 类型：\"{type}\"（允许：background/text/image/group）。");
                    break;
            }
        }
    }

    private static void ValidateBbox(JsonObject layer, int canvasW, int canvasH, string here, List<string> errors)
    {
        var bbox = layer["bbox"] as JsonArray;
        if (bbox is null || bbox.Count != 4)
        {
            errors.Add($"{here}.bbox 必须是长度为 4 的数组 [x,y,w,h]。");
            return;
        }

        var x = TryInt(bbox[0]);
        var y = TryInt(bbox[1]);
        var w = TryInt(bbox[2]);
        var h = TryInt(bbox[3]);

        if (x < 0 || y < 0 || w < 0 || h < 0)
        {
            errors.Add($"{here}.bbox 的 4 个值必须均 ≥ 0。");
            return;
        }

        // 顶层越界检查（canvasW=canvasH=MaxValue 表示当前处于 group 内部，跳过越界判定）
        if (canvasW != int.MaxValue && x + w > canvasW)
        {
            errors.Add($"{here}.bbox 越出画布右边（x+w={x + w} > canvas.w={canvasW}）。");
        }
        if (canvasH != int.MaxValue && y + h > canvasH)
        {
            errors.Add($"{here}.bbox 越出画布下边（y+h={y + h} > canvas.h={canvasH}）。");
        }
    }

    /// <summary>
    /// 校验 editable slot：id 全局唯一 + path 同层唯一；两者都会计入 slotCount。
    /// text/image 有 editable 字段：editable=true 时才算 slot。装饰性的 image (editable=false) 也要 path 同层唯一（但不算 slot）。
    /// </summary>
    private static void ValidateSlot(
        JsonObject layer,
        string here,
        HashSet<string> seenIds,
        HashSet<string> seenPathsInThisLevel,
        ref int slotCount,
        List<string> errors)
    {
        RegisterPath(layer, here, seenPathsInThisLevel, errors);

        var editable = layer["editable"]?.GetValue<bool>() ?? false;
        if (!editable)
        {
            return;
        }

        var id = layer["id"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrEmpty(id))
        {
            errors.Add($"{here} editable=true 必须有 id。");
            return;
        }

        if (!seenIds.Add(id))
        {
            errors.Add($"{here}.id=\"{id}\" 与前面某个 layer 冲突：id 必须全局唯一。");
        }
        else
        {
            slotCount++;
        }
    }

    private static void RegisterPath(JsonObject layer, string here, HashSet<string> seenPathsInThisLevel, List<string> errors)
    {
        var pathVal = layer["path"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrEmpty(pathVal))
        {
            // path 是 slot / group 的语义定位；缺失只对 slot 是硬错，group 缺失只影响 AI 寻址（不硬报）
            return;
        }
        if (!seenPathsInThisLevel.Add(pathVal))
        {
            errors.Add($"{here}.path=\"{pathVal}\" 在同层重复：path 同层唯一。");
        }
    }

    private static int TryInt(JsonNode? node)
    {
        if (node is null) return -1;
        try { return node.GetValue<int>(); }
        catch
        {
            try { return (int)node.GetValue<double>(); }
            catch { return -1; }
        }
    }
}
