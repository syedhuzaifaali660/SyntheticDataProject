using System;
using System.Collections.Generic;

namespace SyntheticData.Generation
{
    public enum ObjectSizeBand { Small, Medium, Large }
    public enum LightingBand { Low, Normal, Bright }

    [Serializable]
    public sealed class CaptureResolution
    {
        public int Width;
        public int Height;

        public CaptureResolution(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }

    [Serializable]
    public sealed class CaptureProfile
    {
        public string Name;
        public float Weight;
        public CaptureResolution[] Resolutions;
        public float HorizontalSafeMargin;
        public float VerticalSafeMargin;

        public CaptureProfile(
            string name,
            float weight,
            float horizontalSafeMargin,
            float verticalSafeMargin,
            params CaptureResolution[] resolutions)
        {
            Name = name;
            Weight = weight;
            HorizontalSafeMargin = horizontalSafeMargin;
            VerticalSafeMargin = verticalSafeMargin;
            Resolutions = resolutions ?? Array.Empty<CaptureResolution>();
        }

        public bool ContainsResolution(int width, int height)
        {
            foreach (var resolution in Resolutions)
            {
                if (resolution.Width == width && resolution.Height == height) return true;
            }
            return false;
        }
    }

    public static class CaptureProfileCatalog
    {
        private static readonly CaptureProfile[] Profiles =
        {
            new CaptureProfile("Square", 0.15f, 0.22f, 0.22f, new CaptureResolution(640, 640)),
            new CaptureProfile("IPhonePortrait", 0.25f, 0.35f, 0.18f, new CaptureResolution(360, 780), new CaptureResolution(540, 1170), new CaptureResolution(720, 1560)),
            new CaptureProfile("IPhoneLandscape", 0.20f, 0.16f, 0.32f, new CaptureResolution(780, 360), new CaptureResolution(1170, 540), new CaptureResolution(1560, 720)),
            new CaptureProfile("Webcam", 0.20f, 0.20f, 0.23f, new CaptureResolution(640, 480), new CaptureResolution(960, 540), new CaptureResolution(1280, 720)),
            new CaptureProfile("LaptopWindow", 0.20f, 0.18f, 0.28f, new CaptureResolution(640, 400), new CaptureResolution(960, 600), new CaptureResolution(1280, 800))
        };

        public static IReadOnlyList<CaptureProfile> All => Profiles;
        public static IReadOnlyList<string> AllowedProfileNames => new[] { "Square", "IPhonePortrait", "IPhoneLandscape", "Webcam", "LaptopWindow" };

        public static CaptureProfile Baseline(int width, int height)
        {
            return new CaptureProfile("Baseline", 1f, 0.1f, 0.1f, new CaptureResolution(width, height));
        }
    }
}
