using PosterMint.Application.Sessions;

namespace PosterMint.Application.Rendering;

public interface IRenderService
{
    string RenderPosterHtml(PosterSessionDto session);

    string RenderPosterHtmlPage(PosterSessionDto session, bool compact = false);
}
