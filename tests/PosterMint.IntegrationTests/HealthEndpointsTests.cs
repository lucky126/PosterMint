using PosterMint.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json.Nodes;

namespace PosterMint.IntegrationTests;

public sealed class HealthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LiveHealthEndpoint_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SessionChatFlow_UpdatesPreviewContent()
    {
        using var client = _factory.CreateClient();

        var templatesEnvelope = await client.GetFromJsonAsync<ApiEnvelope<List<TemplateSummaryContract>>>("/api/shop/templates");
        Assert.NotNull(templatesEnvelope);

        var templateId = templatesEnvelope!.Data.First(x => x.TemplateKey == "single-dish-red").Id;

        var sessionResponse = await client.PostAsJsonAsync("/api/shop/sessions", new
        {
            templateId,
            name = "Integration Session"
        });
        sessionResponse.EnsureSuccessStatusCode();

        var sessionEnvelope = await sessionResponse.Content.ReadFromJsonAsync<ApiEnvelope<SessionContract>>();
        Assert.NotNull(sessionEnvelope);

        var chatResponse = await client.PostAsJsonAsync($"/api/shop/sessions/{sessionEnvelope!.Data.SessionKey}/chat", new
        {
            message = "storeName:Old Street,promoPrice:59.9,dishName加粗一点"
        });
        chatResponse.EnsureSuccessStatusCode();

        var renderHtml = await client.GetStringAsync($"/api/render/{sessionEnvelope.Data.SessionKey}.html");

        Assert.Contains("Old Street", renderHtml);
        Assert.Contains("59.9", renderHtml);
    }

    [Fact]
    public async Task AdminTemplateFlow_CanCreateSubmitAndApproveTemplate()
    {
        using var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/templates", new
        {
            templateKey = $"integration-{Guid.NewGuid():N}",
            name = "审核流测试模板",
            description = "集成测试创建的模板",
            scene = "SingleDish",
            canvas = new JsonObject
            {
                ["width"] = 1080,
                ["height"] = 1920,
                ["background"] = "#8d1f1c"
            },
            fields = new JsonArray
            {
                new JsonObject { ["key"] = "storeName", ["label"] = "店铺名称", ["type"] = "text", ["default"] = "今日招牌" },
                new JsonObject { ["key"] = "promoPrice", ["label"] = "促销价", ["type"] = "price", ["default"] = "59.9" }
            },
            layout = new JsonArray
            {
                new JsonObject { ["type"] = "rect", ["x"] = 0, ["y"] = 0, ["w"] = 1080, ["h"] = 1920, ["fill"] = "#8d1f1c" }
            },
            tags = new[]
            {
                new { dimension = "marketing", tagValue = "限时秒杀", weight = 1.0 }
            }
        });

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<TemplateDetailContract>>();
        Assert.NotNull(created);

        var submitResponse = await client.PostAsync($"/api/admin/templates/{created!.Data.Id}/submit", null);
        submitResponse.EnsureSuccessStatusCode();
        var submitted = await submitResponse.Content.ReadFromJsonAsync<ApiEnvelope<TemplateDetailContract>>();
        Assert.NotNull(submitted);
        Assert.Equal("Pending", submitted!.Data.Status);

        var reviewResponse = await client.PostAsJsonAsync($"/api/admin/templates/{created.Data.Id}/review", new
        {
            approved = true,
            comment = "通过"
        });
        reviewResponse.EnsureSuccessStatusCode();
        var reviewed = await reviewResponse.Content.ReadFromJsonAsync<ApiEnvelope<TemplateDetailContract>>();
        Assert.NotNull(reviewed);
        Assert.Equal("Approved", reviewed!.Data.Status);
    }

    public sealed record ApiEnvelope<T>(T Data, string RequestId, DateTimeOffset Timestamp);

    public sealed record TemplateSummaryContract(int Id, string TemplateKey);

    public sealed record TemplateDetailContract(int Id, string TemplateKey, string Status);

    public sealed record SessionContract(string SessionKey);
}
