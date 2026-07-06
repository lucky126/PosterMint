using PosterMint.Application.Rendering;
using PosterMint.Application.Sessions;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace PosterMint.Infrastructure.Rendering;

public sealed class PosterRenderService : IRenderService
{
    public string RenderPosterHtml(PosterSessionDto session)
    {
        var snapshot = session.TemplateSnapshot as JsonObject ?? new JsonObject();
        var canvas = snapshot["canvas"] as JsonObject ?? new JsonObject();
        var layout = session.Layout as JsonArray ?? [];
        var fields = session.Fields as JsonObject ?? new JsonObject();
        var width = canvas["width"]?.GetValue<int?>() ?? 1080;
        var height = canvas["height"]?.GetValue<int?>() ?? 1920;
        var background = canvas["background"]?.GetValue<string>() ?? "#ffffff";

        var builder = new StringBuilder();
        builder.Append($"""<div class="poster-canvas" data-w="{width}" data-h="{height}" style="position:absolute;left:0;top:0;width:{width}px;height:{height}px;background:{EscapeAttr(background)};overflow:hidden;">""");
        foreach (var node in layout)
        {
            builder.Append(RenderNode(node as JsonObject, fields, new(0, 0), null));
        }
        builder.Append("</div>");
        return builder.ToString();
    }

    public string RenderPosterHtmlPage(PosterSessionDto session, bool compact = false)
    {
        var snapshot = session.TemplateSnapshot as JsonObject ?? new JsonObject();
        var canvas = snapshot["canvas"] as JsonObject ?? new JsonObject();
        var width = canvas["width"]?.GetValue<int?>() ?? 1080;
        var height = canvas["height"]?.GetValue<int?>() ?? 1920;
        var background = canvas["background"]?.GetValue<string>() ?? "#f6f2ec";
        var poster = RenderPosterHtml(session);

        // compact 模式（iframe 预览）：海报以固定 width×height 渲染，用 JS 按 iframe 实际宽度
        // 等比 scale。注意 CSS calc 不能做「长度÷长度」，缩放比必须用 JS 计算。
        if (compact)
        {
            var ratioPercent = (height * 100.0 / width).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
            return $$"""
            <!doctype html>
            <html lang="zh-CN">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>Poster Preview</title>
              <style>
                *{box-sizing:border-box}
                html,body{margin:0;padding:0;width:100%;height:100%;overflow:hidden;background:{{EscapeAttr(background)}}}
                /* 容器高度按画布比例锁定，宽度撑满 iframe */
                .scale{position:relative;width:100%;padding-top:{{ratioPercent}}%;overflow:hidden}
                .scale>.poster-canvas{position:absolute;left:0;top:0;transform-origin:top left}
                .poster-placeholder{display:grid;place-items:center;color:#8a6d60}
              </style>
            </head>
            <body>
              <div class="scale">{{poster}}</div>
              <script>
                (function(){
                  var W={{width}};
                  function fit(){
                    var box=document.querySelector('.scale');
                    var poster=document.querySelector('.poster-canvas');
                    if(!box||!poster)return;
                    poster.style.transform='scale('+(box.clientWidth/W)+')';
                  }
                  fit();
                  window.addEventListener('resize',fit);
                  // srcdoc 下资源/字体加载后尺寸可能变化，二次校正
                  window.addEventListener('load',fit);
                  setTimeout(fit,50);setTimeout(fit,300);
                })();
              </script>
            </body>
            </html>
            """;
        }

        return $$"""
        <!doctype html>
        <html lang="zh-CN">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <title>Poster Preview</title>
          <style>
            *{box-sizing:border-box}
            body{margin:0;background:#f6f2ec;font-family:"Noto Sans SC","Microsoft YaHei UI",sans-serif}
            .stage{min-height:100vh;display:grid;place-items:center;padding:24px}
            .scale{position:relative;overflow:hidden;width:min(100vw,{{width}}px);aspect-ratio:{{width}}/{{height}}}
            .scale>.poster-canvas{position:absolute;left:0;top:0;transform-origin:top left;transform:scale(var(--poster-scale))}
            .poster-placeholder{display:grid;place-items:center;color:#8a6d60;font-size:20px}
          </style>
          <script>
            function resizePoster(){
              const wrap=document.querySelector('.scale');
              if(!wrap)return;
              const available=Math.min(document.documentElement.clientWidth||{{width}}, {{width}});
              document.documentElement.style.setProperty('--poster-scale', available/{{width}});
              wrap.style.width=available+'px';
            }
            window.addEventListener('resize', resizePoster);
            window.addEventListener('load', resizePoster);
          </script>
        </head>
        <body>
          <main class="stage"><div class="scale">{{poster}}</div></main>
        </body>
        </html>
        """;
    }

    private static string RenderNode(JsonObject? node, JsonObject fields, Offset offset, JsonObject? item)
    {
        if (node is null)
        {
            return string.Empty;
        }

        var type = node["type"]?.GetValue<string>() ?? string.Empty;
        var x = GetInt(node, "x") + offset.X;
        var y = GetInt(node, "y") + offset.Y;
        var w = GetInt(node, "w", GetInt(node, "width"));
        var h = GetInt(node, "h", GetInt(node, "height"));
        var radius = GetInt(node, "radius");
        var baseStyle = BuildBaseStyle(node, x, y, w, h, radius);

        return type switch
        {
            "rect" => $"""<div style="{EscapeAttr($"{baseStyle};background:{node["fill"]?.GetValue<string>() ?? "transparent"};{BuildBorder(node)}")}"></div>""",
            "image" or "qrcode" => RenderImage(node, fields, item, baseStyle),
            "text" or "price" => RenderText(node, fields, item, baseStyle),
            "repeat" => RenderRepeat(node, fields, offset, item),
            _ => string.Empty
        };
    }

    private static string RenderImage(JsonObject node, JsonObject fields, JsonObject? item, string baseStyle)
    {
        var source = GetValue(node["field"]?.GetValue<string>(), fields, item);
        var sourceText = source?.GetValue<string>() ?? source?.ToJsonString().Trim('"') ?? string.Empty;
        var style = $"{baseStyle};overflow:hidden;background:{node["background"]?.GetValue<string>() ?? "#f1f1f1"}";
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return $"""<div class="poster-placeholder" style="{EscapeAttr(style)}">{EscapeHtml(node["placeholder"]?.GetValue<string>() ?? "图片")}</div>""";
        }

        var fit = node["fit"]?.GetValue<string>() ?? "cover";
        return $"""<div style="{EscapeAttr(style)}"><img alt="" src="{EscapeAttr(sourceText)}" style="width:100%;height:100%;object-fit:{EscapeAttr(fit)};display:block;"></div>""";
    }

    private static string RenderText(JsonObject node, JsonObject fields, JsonObject? item, string baseStyle)
    {
        var value = GetTextValue(node, fields, item);
        var fontSize = FitFontSize(value, node);
        var style = string.Join(';', new[]
        {
            baseStyle,
            $"font-size:{fontSize}px",
            $"font-weight:{node["fontWeight"]?.GetValue<int?>() ?? 400}",
            $"line-height:{node["lineHeight"]?.GetValue<double?>() ?? 1.16}",
            $"color:{node["color"]?.GetValue<string>() ?? "#111"}",
            $"text-align:{node["align"]?.GetValue<string>() ?? "left"}",
            $"letter-spacing:{node["letterSpacing"]?.GetValue<int?>() ?? 0}px",
            "font-family:\"Noto Sans SC\",\"Microsoft YaHei UI\",sans-serif",
            "overflow:hidden",
            "white-space:pre-wrap",
            "word-break:break-word"
        });

        return $"""<div style="{EscapeAttr(style)}">{EscapeHtml(value)}</div>""";
    }

    private static string RenderRepeat(JsonObject node, JsonObject fields, Offset offset, JsonObject? item)
    {
        var value = GetValue(node["field"]?.GetValue<string>(), fields, item);
        var items = value as JsonArray ?? [];
        var maxItems = node["maxItems"]?.GetValue<int?>() ?? items.Count;
        var columns = node["columns"]?.GetValue<int?>() ?? 1;
        var gapX = node["gapX"]?.GetValue<int?>() ?? 0;
        var gapY = node["gapY"]?.GetValue<int?>() ?? 0;
        var totalWidth = GetInt(node, "w");
        var itemWidth = node["itemWidth"]?.GetValue<int?>() ?? ((totalWidth - gapX * Math.Max(columns - 1, 0)) / Math.Max(columns, 1));
        var itemHeight = node["itemHeight"]?.GetValue<int?>() ?? 120;
        var children = node["children"] as JsonArray ?? [];
        var builder = new StringBuilder();

        for (var index = 0; index < Math.Min(items.Count, maxItems); index++)
        {
            var col = index % columns;
            var row = index / columns;
            var childOffset = new Offset(
                GetInt(node, "x") + offset.X + col * (itemWidth + gapX),
                GetInt(node, "y") + offset.Y + row * (itemHeight + gapY));
            var normalizedItem = items[index] as JsonObject;
            foreach (var child in children)
            {
                builder.Append(RenderNode(child as JsonObject, fields, childOffset, normalizedItem));
            }
        }

        return builder.ToString();
    }

    private static string GetTextValue(JsonObject node, JsonObject fields, JsonObject? item)
    {
        var raw = GetValue(node["field"]?.GetValue<string>(), fields, item) ?? node["text"];
        var text = raw?.GetValue<string>() ?? raw?.ToJsonString().Trim('"') ?? string.Empty;
        if ((node["type"]?.GetValue<string>() ?? string.Empty) == "price" && !string.IsNullOrWhiteSpace(text))
        {
            return $"{node["prefix"]?.GetValue<string>() ?? "￥"}{text}";
        }

        return text;
    }

    private static int FitFontSize(string text, JsonObject node)
    {
        var size = node["fontSize"]?.GetValue<int?>() ?? 32;
        var min = node["minFontSize"]?.GetValue<int?>() ?? Math.Max(16, (int)(size * 0.72));
        var maxLines = node["maxLines"]?.GetValue<int?>() ?? 1;
        var width = GetInt(node, "w", 300);

        while (size > min)
        {
            var charsPerLine = Math.Max(1, (int)Math.Floor(width / (size * 0.58)));
            var lines = text.Split('\n')
                .Sum(line => (int)Math.Ceiling(Math.Max(1, line.Length) / (double)charsPerLine));

            if (lines <= maxLines)
            {
                break;
            }

            size -= 2;
        }

        return size;
    }

    private static JsonNode? GetValue(string? field, JsonObject fields, JsonObject? item)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return null;
        }

        if (field == "$value")
        {
            return item;
        }

        if (item is not null)
        {
            var fromItem = Traverse(field, item);
            if (fromItem is not null)
            {
                return fromItem;
            }
        }

        return Traverse(field, fields);
    }

    private static JsonNode? Traverse(string path, JsonObject source)
    {
        JsonNode? current = source;
        foreach (var segment in path.Split('.'))
        {
            current = current?[segment];
            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static string BuildBaseStyle(JsonObject node, int x, int y, int w, int h, int radius)
    {
        var parts = new List<string>
        {
            "position:absolute",
            $"left:{x}px",
            $"top:{y}px"
        };

        if (w > 0) parts.Add($"width:{w}px");
        if (h > 0) parts.Add($"height:{h}px");
        if (radius > 0) parts.Add($"border-radius:{radius}px");
        if (node["opacity"] is not null) parts.Add($"opacity:{node["opacity"]!.GetValue<double>()}");

        return string.Join(';', parts);
    }

    private static string BuildBorder(JsonObject node)
    {
        var stroke = node["stroke"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(stroke))
        {
            return string.Empty;
        }

        var width = node["strokeWidth"]?.GetValue<int?>() ?? 1;
        return $"border:{width}px solid {stroke}";
    }

    private static int GetInt(JsonObject node, string key, int fallback = 0) =>
        node[key]?.GetValue<int?>() ?? fallback;

    private static string EscapeHtml(string value) => WebUtility.HtmlEncode(value);

    private static string EscapeAttr(string value) => WebUtility.HtmlEncode(value);

    private readonly record struct Offset(int X, int Y);
}
