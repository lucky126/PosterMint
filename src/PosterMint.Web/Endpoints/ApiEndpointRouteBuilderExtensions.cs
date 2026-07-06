using PosterMint.Application.AI;
using PosterMint.Application.Configs;
using PosterMint.Application.Contracts;
using PosterMint.Application.PspTemplates;
using PosterMint.Application.Shops;
using PosterMint.Domain.Enums;
using PosterMint.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace PosterMint.Web.Endpoints;

public static class ApiEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapPosterMintApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/health/live", (HttpContext context) =>
            Results.Ok(ApiEnvelope.Create(
                new { status = "ok", service = "PosterMint.Web" },
                context.TraceIdentifier)));

        api.MapGet("/health/ready", async (
            HttpContext context,
            PosterMintDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return Results.Problem(
                    title: "Readiness check failed.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var templateCount = await dbContext.Templates.CountAsync(cancellationToken);
            return Results.Ok(ApiEnvelope.Create(
                new { status = "ready", templateCount },
                context.TraceIdentifier));
        });

        // ------------ 配置 / AI 状态 ------------
        api.MapGet("/admin/configs", async (
            HttpContext context,
            IConfigService configService,
            CancellationToken cancellationToken) =>
        {
            var configs = await configService.ListAsync(cancellationToken);
            return Results.Ok(ApiEnvelope.Create(configs, context.TraceIdentifier));
        });

        api.MapGet("/admin/ai/status", (
            HttpContext context,
            IChatEditingAiService aiService) =>
        {
            return Results.Ok(ApiEnvelope.Create(aiService.GetStatus(), context.TraceIdentifier));
        });

        // ------------ 商户管理（PC 后台）------------
        api.MapGet("/admin/shops", async (
            string? keyword,
            IShopService shopService,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var shops = await shopService.ListAsync(keyword, cancellationToken);
            return Results.Ok(ApiEnvelope.Create(shops, context.TraceIdentifier));
        });

        api.MapGet("/admin/shops/{id:int}", async (
            int id,
            IShopService shopService,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var shop = await shopService.GetAsync(id, cancellationToken);
            return shop is null
                ? Results.NotFound()
                : Results.Ok(ApiEnvelope.Create(shop, context.TraceIdentifier));
        });

        api.MapPost("/admin/shops", async (
            ShopUpsertRequest request,
            IShopService shopService,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var shop = await shopService.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/admin/shops/{shop.Id}", ApiEnvelope.Create(shop, context.TraceIdentifier));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        api.MapPut("/admin/shops/{id:int}", async (
            int id,
            ShopUpsertRequest request,
            IShopService shopService,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var shop = await shopService.UpdateAsync(id, request, cancellationToken);
                return Results.Ok(ApiEnvelope.Create(shop, context.TraceIdentifier));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        api.MapDelete("/admin/shops/{id:int}", async (
            int id,
            IShopService shopService,
            CancellationToken cancellationToken) =>
        {
            var deleted = await shopService.DeleteAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        // ------------ 商铺登录（小程序端调用） ------------
        api.MapPost("/shop/login", async (
            ShopLoginRequest request,
            HttpContext context,
            IShopAuthService auth,
            CancellationToken cancellationToken) =>
        {
            var ip = context.Connection.RemoteIpAddress?.ToString();
            if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var xff) && xff.Count > 0)
            {
                var first = xff.ToString().Split(',').FirstOrDefault()?.Trim();
                if (!string.IsNullOrEmpty(first)) ip = first;
            }
            var ua = context.Request.Headers.UserAgent.ToString();

            var result = await auth.LoginAsync(request, ip, ua, cancellationToken);
            if (result.Result == ShopLoginResult.Success)
            {
                return Results.Ok(ApiEnvelope.Create(result, context.TraceIdentifier));
            }
            return Results.Json(
                new
                {
                    error = result.Result.ToString(),
                    message = result.Message,
                    traceId = context.TraceIdentifier
                },
                statusCode: StatusCodes.Status401Unauthorized);
        });

        // ------------ 商铺登录日志（PC 后台） ------------
        api.MapGet("/admin/shops/{id:int}/login-logs", async (
            int id,
            int? limit,
            HttpContext context,
            IShopAuthService auth,
            CancellationToken cancellationToken) =>
        {
            var logs = await auth.ListLogsAsync(id, limit ?? 50, cancellationToken);
            return Results.Ok(ApiEnvelope.Create(logs, context.TraceIdentifier));
        });

        // ------------ PSP 模板管理（CC 类工具产出的模板 JSON 入库） ------------
        api.MapGet("/admin/psp-templates", async (
            HttpContext context,
            IPspTemplateService pspService,
            TemplateOwnership? ownership,
            int? shopId,
            TemplateSceneType? scene,
            string? keyword,
            CancellationToken cancellationToken) =>
        {
            var rows = await pspService.ListAsync(
                new PspTemplateFilter(ownership, shopId, scene, keyword),
                cancellationToken);
            return Results.Ok(ApiEnvelope.Create(rows, context.TraceIdentifier));
        });

        api.MapGet("/admin/psp-templates/{id:int}", async (
            int id,
            HttpContext context,
            IPspTemplateService pspService,
            CancellationToken cancellationToken) =>
        {
            var detail = await pspService.GetAsync(id, cancellationToken);
            return detail is null
                ? Results.NotFound(ApiErrorResponse.Create("PspTemplateNotFound", $"PSP template '{id}' not found.", context.TraceIdentifier))
                : Results.Ok(ApiEnvelope.Create(detail, context.TraceIdentifier));
        });

        api.MapPost("/admin/psp-templates/validate", (
            JsonNode? psp,
            HttpContext context,
            IPspTemplateService pspService) =>
        {
            var result = pspService.Validate(psp);
            return Results.Ok(ApiEnvelope.Create(result, context.TraceIdentifier));
        });

        api.MapPost("/admin/psp-templates/import", async (
            PspTemplateImportRequest request,
            HttpContext context,
            IPspTemplateService pspService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var detail = await pspService.ImportAsync(request, cancellationToken);
                return Results.Created(
                    $"/api/admin/psp-templates/{detail.Id}",
                    ApiEnvelope.Create(detail, context.TraceIdentifier));
            }
            catch (PspValidationException ex)
            {
                return Results.BadRequest(new
                {
                    error = "PspValidationFailed",
                    message = "PSP JSON validation failed.",
                    result = ex.Result,
                    traceId = context.TraceIdentifier
                });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ApiErrorResponse.Create("ValidationError", ex.Message, context.TraceIdentifier));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiErrorResponse.Create("ValidationError", ex.Message, context.TraceIdentifier));
            }
        });

        api.MapDelete("/admin/psp-templates/{id:int}", async (
            int id,
            IPspTemplateService pspService,
            CancellationToken cancellationToken) =>
        {
            var deleted = await pspService.DeleteAsync(id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }
}
