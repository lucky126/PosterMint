using PosterMint.Application.Templates;
using PosterMint.Domain.Enums;
using System.Text.Json.Nodes;

namespace PosterMint.Web.Components.Pages;

internal static class StarterTemplateFactory
{
    public static CreateTemplateRequest Create(TemplateSceneType scene, string? goal = null)
    {
        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var resolvedGoal = string.IsNullOrWhiteSpace(goal) ? GetDefaultGoal(scene) : goal.Trim();

        return scene switch
        {
            TemplateSceneType.Menu => new CreateTemplateRequest
            {
                TemplateKey = $"menu-{suffix}",
                Name = "新菜单模板",
                Description = resolvedGoal,
                Scene = TemplateSceneType.Menu,
                Canvas = new JsonObject { ["width"] = 1080, ["height"] = 1920, ["background"] = "#f7efe0" },
                Fields = new JsonArray
                {
                    new JsonObject { ["key"] = "storeName", ["label"] = "店铺名称", ["type"] = "text", ["default"] = "本店菜单" },
                    new JsonObject { ["key"] = "title", ["label"] = "菜单标题", ["type"] = "text", ["default"] = "招牌菜单" },
                    new JsonObject { ["key"] = "items", ["label"] = "菜品列表", ["type"] = "items", ["required"] = true }
                },
                Layout = new JsonArray
                {
                    new JsonObject { ["type"] = "rect", ["x"] = 0, ["y"] = 0, ["w"] = 1080, ["h"] = 1920, ["fill"] = "#f7efe0" },
                    new JsonObject { ["type"] = "text", ["field"] = "storeName", ["x"] = 96, ["y"] = 96, ["w"] = 420, ["h"] = 44, ["fontSize"] = 30, ["fontWeight"] = 700, ["color"] = "#214f3d" },
                    new JsonObject { ["type"] = "text", ["field"] = "title", ["x"] = 96, ["y"] = 156, ["w"] = 760, ["h"] = 84, ["fontSize"] = 58, ["fontWeight"] = 900, ["color"] = "#214f3d" }
                },
                Tags =
                [
                    new TemplateTagDto("product", "店铺爆款", 1.0d),
                    new TemplateTagDto("crowd", "多人聚餐", 1.0d)
                ]
            },
            _ => new CreateTemplateRequest
            {
                TemplateKey = $"single-dish-{suffix}",
                Name = "新单品模板",
                Description = resolvedGoal,
                Scene = TemplateSceneType.SingleDish,
                Canvas = new JsonObject { ["width"] = 1080, ["height"] = 1920, ["background"] = "#8d1f1c" },
                Fields = new JsonArray
                {
                    new JsonObject { ["key"] = "storeName", ["label"] = "店铺名称", ["type"] = "text", ["default"] = "今日招牌" },
                    new JsonObject { ["key"] = "dishName", ["label"] = "菜品名称", ["type"] = "text", ["required"] = true },
                    new JsonObject { ["key"] = "promoPrice", ["label"] = "促销价", ["type"] = "price", ["required"] = true },
                    new JsonObject { ["key"] = "qrcode", ["label"] = "二维码", ["type"] = "qrcode" }
                },
                Layout = new JsonArray
                {
                    new JsonObject { ["type"] = "rect", ["x"] = 0, ["y"] = 0, ["w"] = 1080, ["h"] = 1920, ["fill"] = "#8d1f1c" },
                    new JsonObject { ["type"] = "text", ["field"] = "storeName", ["x"] = 120, ["y"] = 140, ["w"] = 840, ["h"] = 72, ["fontSize"] = 42, ["fontWeight"] = 700, ["color"] = "#fff6df", ["align"] = "center" },
                    new JsonObject { ["type"] = "text", ["field"] = "dishName", ["x"] = 120, ["y"] = 980, ["w"] = 840, ["h"] = 88, ["fontSize"] = 74, ["fontWeight"] = 900, ["color"] = "#fff6df", ["align"] = "center" },
                    new JsonObject { ["type"] = "price", ["field"] = "promoPrice", ["x"] = 300, ["y"] = 1120, ["w"] = 420, ["h"] = 110, ["fontSize"] = 96, ["fontWeight"] = 900, ["color"] = "#ffe071", ["align"] = "center" }
                },
                Tags =
                [
                    new TemplateTagDto("product", "招牌必点", 1.0d),
                    new TemplateTagDto("marketing", "限时秒杀", 1.0d)
                ]
            }
        };
    }

    private static string GetDefaultGoal(TemplateSceneType scene) => scene switch
    {
        TemplateSceneType.Menu => "做一张适合多人聚餐和套餐展示的菜单模板",
        TemplateSceneType.StorePromo => "做一张适合门店宣传和引流的门店模板",
        TemplateSceneType.Festival => "做一张适合节庆活动和限时促销的模板",
        _ => "做一张适合主推单品和活动价展示的促销模板"
    };
}
