using PosterMint.Application.Recommendations;

namespace PosterMint.UnitTests;

public sealed class TemplateRecommendationScoreCalculatorTests
{
    [Fact]
    public void Calculate_ReturnsWeightedScore_ForMatchedTags()
    {
        var calculator = new TemplateRecommendationScoreCalculator();
        var templateTags = new[]
        {
            new TagContextItem("shopType", "火锅", 1.0d),
            new TagContextItem("marketing", "限时秒杀", 1.0d),
            new TagContextItem("festival", "春节", 0.5d)
        };

        var contextTags = new[]
        {
            new TagContextItem("shopType", "火锅", 1.0d),
            new TagContextItem("marketing", "限时秒杀", 2.0d),
            new TagContextItem("weather", "寒冬暖食", 1.0d)
        };

        var score = calculator.Calculate(templateTags, contextTags);

        Assert.Equal(24, score);
    }
}
