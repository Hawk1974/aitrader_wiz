using AiTrader.Wiz.Core;

namespace AiTrader.Wiz.UnitTests;

public sealed class PathDisplayFormatterTests
{
    [Fact]
    public void CompactPath_TruncatesLongWindowsPathToTrailingSegments()
    {
        var path = @"C:\Users\hawkc\AppData\Local\AlTrader\ConfigWizard\Logs\AlTraderConfigWizard_20260529T230846Z.log";

        var displayPath = PathDisplayFormatter.CompactPath(path);

        Assert.Equal(@"...\AlTrader\ConfigWizard\Logs\AlTraderConfigWizard_20260529T230846Z.log", displayPath);
    }

    [Fact]
    public void CompactPath_LeavesShortPathsUnchanged()
    {
        var path = @"C:\Logs\AlTraderConfigWizard.log";

        var displayPath = PathDisplayFormatter.CompactPath(path);

        Assert.Equal(path, displayPath);
    }
}
