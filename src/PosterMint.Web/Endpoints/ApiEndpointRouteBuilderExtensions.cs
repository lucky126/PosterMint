using PosterMint.Application.AI;
using PosterMint.Application.Configs;
using PosterMint.Application.Contracts;
using PosterMint.Application.Rendering;
using PosterMint.Application.Sessions;
using PosterMint.Application.Shops;
using PosterMint.Application.Templates;
using PosterMint.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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

        api.MapGet("/templates", async (
            HttpContext context,
            ITemplateService templateService,
            CancellationToken cancellationToken) =>
        {
            var templates = await templateService.ListAsync(cancellationToken);
            return Results.Ok(ApiEnvelope.Create(templates, context.TraceIdentifier));
        });

        api.MapGet("/shop/templates", async (
            HttpContext context,
            ITemplateService templateService,
            CancellationToken cancellationToken) =>
        {
            var templates = await templateService.ListAsync(cancellationToken);
            return Results.Ok(ApiEnvelope.Create(templates, context.TraceIdentifier));
        });

        api.MapGet("/templates/{id:int}", async (
            int id,
            HttpContext context,
            ITemplateService templateService,
            CancellationToken cancellationToken) =>
        {
            var template = await templateService.GetAsync(id, cancellationToken);
            return template is null
                ? Results.NotFound(ApiErrorResponse.Create(
                    "TemplateNotFound",
                    $"Template '{id}' was not found.",
                    context.TraceIdentifier))
                : Results.Ok(ApiEnvelope.Create(template, context.TraceIdentifier));
        });

        api.MapGet("/admin/categories", async (
            HttpContext context,
            ITemplateService templateService,
            CancellationToken cancellationToken) =>
        {
            var categories = await templateService.GetAdminCategoriesAsync(cancellationToken);
            return Results.Ok(ApiEnvelope.Create(categories, context.TraceIdentifier));
        });

        api.MapGet("/admin/templates", async (
            HttpContext context,
            ITemplateService templateService,
            CancellationToken cancellationToken) =>
        {
            var templates = await templateService.ListAsync(cancellationToken);
            return Results.Ok(ApiEnvelope.Create(templates, context.TraceIdentifier));
        });

        api.MapPost("/templates", async (
            CreateTemplateRequest request,
            HttpContext context,
            ITemplateService templateService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var template = await templateService.CreateAsync(request, cancellationToken);
                return Results.Created(
                    $"/api/templates/{template.Id}",
                    ApiEnvelope.Create(template, context.TraceIdentifier));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiErrorResponse.Create(
                    "ValidationError",
                    ex.Message,
                    context.TraceIdentifier));
            }
        });

        api.MapPut("/admin/templates/{id:int}", async (
            int id,
            UpdateTemplateRequest request,
            HttpContext context,
            ITemplateService templateService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var template = await templateService.UpdateAsync(id, request, cancellationToken);
                return Results.Ok(ApiEnvelope.Create(template, context.TraceIdentifier));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiErrorResponse.Create(
                    "ValidationError",
                    ex.Message,
                    context.TraceIdentifier));
            }
        });

        api.MapPost("/admin/templates/{id:int}/submit", async (
            int id,
            HttpContext context,
            ITemplateService templateService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var template = await templateService.SubmitAsync(id, cancellationToken);
                return Results.Ok(ApiEnvelope.Create(template, context.TraceIdentifier));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiErrorResponse.Create(
                    "ValidationError",
                    ex.Message,
                    context.TraceIdentifier));
            }
        });

        api.MapPost("/admin/templates/{id:int}/review", async (
            int id,
            ReviewTemplateRequest request,
            HttpContext context,
            ITemplateService templateService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var template = await templateService.ReviewAsync(id, request, cancellationToken);
                return Results.Ok(ApiEnvelope.Create(template, context.TraceIdentifier));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiErrorResponse.Create(
                    "ValidationError",
                    ex.Message,
                    context.TraceIdentifier));
            }
        });

        api.MapPost("/shop/sessions", async (
            CreateSessionRequest request,
            HttpContext context,
            ISessionService sessionService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var session = await sessionService.CreateAsync(request, cancellationToken);
                return Results.Created(
                    $"/api/shop/sessions/{session.SessionKey}",
                    ApiEnvelope.Create(session, context.TraceIdentifier));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiErrorResponse.Create(
                    "ValidationError",
                    ex.Message,
                    context.TraceIdentifier));
            }
        });

        api.MapPost("/shop/sessions/bootstrap", async (
            BootstrapSessionRequest request,
            HttpContext context,
            ISessionService sessionService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var session = await sessionService.BootstrapAsync(request, cancellationToken);
                return Results.Created(
                    $"/api/shop/sessions/{session.SessionKey}",
                    ApiEnvelope.Create(session, context.TraceIdentifier));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiErrorResponse.Create(
                    "ValidationError",
                    ex.Message,
                    context.TraceIdentifier));
            }
        });

        api.MapGet("/shop/sessions/{sessionKey}", async (
            string sessionKey,
            HttpContext context,
            ISessionService sessionService,
            CancellationToken cancellationToken) =>
        {
            var session = await sessionService.GetAsync(sessionKey, cancellationToken);
            return session is null
                ? Results.NotFound(ApiErrorResponse.Create(
                    "SessionNotFound",
                    $"Session '{sessionKey}' was not found.",
                    context.TraceIdentifier))
                : Results.Ok(ApiEnvelope.Create(session, context.TraceIdentifier));
        });

        api.MapPut("/shop/sessions/{sessionKey}/fields", async (
            string sessionKey,
            UpdateSessionFieldsRequest request,
            HttpContext context,
            ISessionService sessionService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var session = await sessionService.UpdateFieldsAsync(sessionKey, request, cancellationToken);
                return Results.Ok(ApiEnvelope.Create(session, context.TraceIdentifier));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiErrorResponse.Create(
                    "ValidationError",
                    ex.Message,
                    context.TraceIdentifier));
            }
        });

        api.MapPost("/shop/sessions/{sessionKey}/chat", async (
            string sessionKey,
            ApplySessionChatRequest request,
            HttpContext context,
            ISessionService sessionService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await sessionService.ApplyChatAsync(sessionKey, request, cancellationToken);
                return Results.Ok(ApiEnvelope.Create(result, context.TraceIdentifier));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiErrorResponse.Create(
                    "ValidationError",
                    ex.Message,
                    context.TraceIdentifier));
            }
        });

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

        api.MapGet("/render/{sessionKey}.html", async (
            string sessionKey,
            ISessionService sessionService,
            IRenderService renderService,
            CancellationToken cancellationToken) =>
        {
            var session = await sessionService.GetAsync(sessionKey, cancellationToken);
            return session is null
                ? Results.NotFound("Session not found.")
                : Results.Content(renderService.RenderPosterHtmlPage(session, compact: true), "text/html; charset=utf-8");
        });

        // ------------ 商户管理 ------------
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

        return app;
    }
}
