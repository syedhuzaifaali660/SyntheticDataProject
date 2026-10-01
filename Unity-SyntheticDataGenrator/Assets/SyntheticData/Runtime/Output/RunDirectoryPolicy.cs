using System;
using System.IO;

namespace SyntheticData.Output
{
    public static class RunDirectoryPolicy
    {
        public static string Resolve(string outputRoot, string runId)
        {
            if (string.IsNullOrWhiteSpace(outputRoot))
            {
                throw new ArgumentException("An output root is required.", nameof(outputRoot));
            }

            if (string.IsNullOrWhiteSpace(runId) ||
                runId == "." ||
                runId == ".." ||
                runId.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/', '\\' }) >= 0 ||
                runId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException(
                    "A non-empty run ID without path separators or dot segments is required.",
                    nameof(runId));
            }

            var normalizedRoot = TrimTrailingSeparators(Path.GetFullPath(outputRoot));
            var runDirectory = Path.GetFullPath(Path.Combine(normalizedRoot, runId));
            var comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!string.Equals(Path.GetDirectoryName(runDirectory), normalizedRoot, comparison))
            {
                throw new ArgumentException(
                    "The run directory must remain directly under the selected output root.",
                    nameof(runId));
            }

            return runDirectory;
        }

        private static string TrimTrailingSeparators(string path)
        {
            var root = Path.GetPathRoot(path);
            return path.Length > root.Length
                ? path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : path;
        }
    }
}
