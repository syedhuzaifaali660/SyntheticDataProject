using System;
using System.IO;

namespace SyntheticData.Output
{
    public sealed class CapturePaths
    {
        public CapturePaths(string runDirectory, int frameId)
        {
            if (string.IsNullOrWhiteSpace(runDirectory))
            {
                throw new ArgumentException("A run directory is required.", nameof(runDirectory));
            }

            if (frameId <= 0 || frameId > 999999)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frameId),
                    "Frame IDs must be between 1 and 999999.");
            }

            RunDirectory = runDirectory;
            ImagesDirectory = Path.Combine(runDirectory, "images");
            LabelsDirectory = Path.Combine(runDirectory, "labels");
            var frameName = "frame_" + frameId.ToString("D6");
            ImagePath = Path.Combine(ImagesDirectory, frameName + ".png");
            LabelPath = Path.Combine(LabelsDirectory, frameName + ".txt");
            ImageTemporaryPath = ImagePath + ".tmp";
            LabelTemporaryPath = LabelPath + ".tmp";
            ManifestPath = Path.Combine(runDirectory, "manifest.jsonl");
            RunConfigPath = Path.Combine(runDirectory, "run-config.json");
        }

        public string RunDirectory { get; }
        public string ImagesDirectory { get; }
        public string LabelsDirectory { get; }
        public string ImagePath { get; }
        public string LabelPath { get; }
        public string ImageTemporaryPath { get; }
        public string LabelTemporaryPath { get; }
        public string ManifestPath { get; }
        public string RunConfigPath { get; }
    }
}
