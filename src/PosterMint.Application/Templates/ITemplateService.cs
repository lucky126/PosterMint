namespace PosterMint.Application.Templates;

public interface ITemplateService
{
    Task<IReadOnlyList<TemplateSummaryDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminCategorySummaryDto>> GetAdminCategoriesAsync(CancellationToken cancellationToken = default);

    Task<TemplateDetailDto?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<TemplateDetailDto> CreateAsync(CreateTemplateRequest request, CancellationToken cancellationToken = default);

    Task<TemplateDetailDto> UpdateAsync(int id, UpdateTemplateRequest request, CancellationToken cancellationToken = default);

    Task<TemplateChatResultDto> ApplyInstructionAsync(int id, ApplyTemplateInstructionRequest request, CancellationToken cancellationToken = default);

    Task<TemplateDetailDto> SubmitAsync(int id, CancellationToken cancellationToken = default);

    Task<TemplateDetailDto> ReviewAsync(int id, ReviewTemplateRequest request, CancellationToken cancellationToken = default);
}
