namespace AiTrader.Wiz.Core;

public static class PathDisplayFormatter
{
    public static string CompactPath(string fullPath, int trailingSegmentCount = 4)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return string.Empty;
        }

        var root = Path.GetPathRoot(fullPath) ?? string.Empty;
        var relativeSegments = fullPath[root.Length..]
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

        if (relativeSegments.Length <= trailingSegmentCount)
        {
            return fullPath;
        }

        return $"...{Path.DirectorySeparatorChar}{string.Join(Path.DirectorySeparatorChar, relativeSegments[^trailingSegmentCount..])}";
    }
}
