namespace PosterMint.Application.AI;

/// <summary>
/// AI 服务接口。v2 一期只暴露"连接状态"和"连接测试"给 PC 端 AiSetup 页面用；
/// 具体的 PSP slot patch 生成逻辑在小程序端后续接入时再补 ApplyPspPatch 方法。
/// </summary>
public interface IChatEditingAiService
{
    Task<AiTestResultDto> TestConnectionAsync(CancellationToken cancellationToken = default);

    LlmStatusDto GetStatus();
}
