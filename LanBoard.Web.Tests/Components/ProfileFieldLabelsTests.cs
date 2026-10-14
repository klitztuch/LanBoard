using System.Globalization;
using LanBoard.Web.Components;

namespace LanBoard.Web.Tests.Components;

public class ProfileFieldLabelsTests
{
    private static string Resolve(string uiCulture, Dictionary<string, string> labels, string defaultCulture = "en")
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(uiCulture);
            return ProfileFieldLabels.For(labels, "shirt-size", defaultCulture);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void CurrentCulture_Hit()
        => Assert.Equal("T-Shirt-Größe", Resolve("de", new() { ["en"] = "T-shirt size", ["de"] = "T-Shirt-Größe" }));

    [Fact]
    public void SpecificCulture_FallsBackToParent()
        => Assert.Equal("T-Shirt-Größe", Resolve("de-AT", new() { ["en"] = "T-shirt size", ["de"] = "T-Shirt-Größe" }));

    [Fact]
    public void MissingCulture_FallsBackToDefaultCulture()
        => Assert.Equal("T-shirt size", Resolve("de", new() { ["en"] = "T-shirt size" }));

    [Fact]
    public void BlankLabel_IsTreatedAsMissing()
        => Assert.Equal("T-shirt size", Resolve("de", new() { ["en"] = "T-shirt size", ["de"] = " " }));

    [Fact]
    public void NoUsableLabel_FallsBackToKey()
        => Assert.Equal("shirt-size", Resolve("de", new() { ["fr"] = "Taille" }));
}
