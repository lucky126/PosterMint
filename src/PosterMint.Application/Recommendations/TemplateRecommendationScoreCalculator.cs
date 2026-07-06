namespace PosterMint.Application.Recommendations;

public sealed class TemplateRecommendationScoreCalculator
{
    private static readonly IReadOnlyDictionary<string, double> DimensionWeights =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["shopType"] = 10,
            ["festival"] = 8,
            ["marketing"] = 7,
            ["crowd"] = 5,
            ["consumption"] = 4,
            ["time"] = 3,
            ["period"] = 3,
            ["weather"] = 2,
            ["solarTerm"] = 1
        };

    public double Calculate(
        IEnumerable<TagContextItem> templateTags,
        IEnumerable<TagContextItem> contextTags)
    {
        ArgumentNullException.ThrowIfNull(templateTags);
        ArgumentNullException.ThrowIfNull(contextTags);

        var normalizedTemplateTags = templateTags.ToList();
        double score = 0;

        foreach (var contextTag in contextTags)
        {
            var matchedTag = normalizedTemplateTags.FirstOrDefault(tag =>
                string.Equals(tag.Dimension, contextTag.Dimension, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(tag.TagValue, contextTag.TagValue, StringComparison.OrdinalIgnoreCase));

            if (matchedTag is null)
            {
                continue;
            }

            score += DimensionWeights.GetValueOrDefault(contextTag.Dimension, 1.0d) *
                     matchedTag.Weight *
                     contextTag.Weight;
        }

        return score;
    }
}
