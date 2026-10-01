using System;
using UnityEngine;

namespace SyntheticData.Generation
{
    [CreateAssetMenu(
        fileName = "GenerationConfig",
        menuName = "Synthetic Data/Generation Config")]
    public sealed class GenerationConfig : ScriptableObject
    {
        public int Seed = 42;
        public int Width = 640;
        public int Height = 640;
        public bool UseMixedDeviceProfiles;
        public int BackgroundCount = 98;
        public int MinimumObjects = 0;
        public int MaximumObjects = 3;
        public float MinimumFov = 40f;
        public float MaximumFov = 75f;
        public float MinimumDistance = 1.5f;
        public float MaximumDistance = 5f;
        public float MaximumYaw = 35f;
        public float MaximumPitch = 20f;
        public float MaximumRoll = 12f;
        public float MinimumCenterSeparation = 0.18f;
        public float MinimumBoxPixels = 24f;
        public float MaximumCropFraction = 0.25f;
        public float MaximumProjectedBoxContainmentCoverage = 1f;
        public int MaximumFrameRetries = 20;
        public float MaximumBackgroundCropOffset = 0.1f;
        public float MinimumBackgroundScale = 1f;
        public float MaximumBackgroundScale = 1.3f;
        public float MinimumBackgroundBrightness = 0.8f;
        public float MaximumBackgroundBrightness = 1.2f;
        public float MinimumBackgroundContrast = 0.85f;
        public float MaximumBackgroundContrast = 1.15f;
        public float MaximumBackgroundBlur = 1f;
        public float MinimumBackgroundTemperature = 4500f;
        public float MaximumBackgroundTemperature = 7500f;

        public static GenerationConfig CreateTestDefault(int seed)
        {
            var config = CreateInstance<GenerationConfig>();
            config.Seed = seed;
            return config;
        }

        public static GenerationConfig CreateMixedDeviceDefault(int seed)
        {
            var config = CreateTestDefault(seed);
            config.ApplyMixedDeviceSettings();
            return config;
        }

        public void ApplyMixedDeviceSettings()
        {
            var config = this;
            config.UseMixedDeviceProfiles = true;
            // The small-object band intentionally includes 8-24px targets.
            // Keep the projector gate aligned with that band for this run only.
            config.MinimumBoxPixels = 8f;
            config.MaximumProjectedBoxContainmentCoverage = 0.95f;
            config.MinimumBackgroundBrightness = 0.55f;
            config.MaximumBackgroundBrightness = 1.35f;
            config.MinimumBackgroundContrast = 0.70f;
            config.MaximumBackgroundContrast = 1.30f;
            config.MaximumBackgroundBlur = 2f;
            config.MinimumBackgroundTemperature = 2800f;
            config.MaximumBackgroundTemperature = 8500f;
        }

        public void ValidateOrThrow()
        {
            if (TryGetValidationError(out var error))
            {
                throw new InvalidOperationException(error);
            }
        }

        private bool TryGetValidationError(out string error)
        {
            if (!AllFloatsAreFinite())
            {
                error = "All floating-point generation settings must be finite.";
                return true;
            }

            if (Width <= 0 || Height <= 0)
            {
                error = "Image dimensions must be positive.";
                return true;
            }

            if (BackgroundCount <= 0)
            {
                error = "At least one background is required.";
                return true;
            }

            if (MinimumObjects < 0 || MaximumObjects < MinimumObjects || MaximumObjects > 3)
            {
                error = "Object counts must satisfy 0 <= minimum <= maximum <= 3.";
                return true;
            }

            if (MinimumFov <= 0f || MaximumFov < MinimumFov || MaximumFov >= 180f)
            {
                error = "Camera FOV must satisfy 0 < minimum <= maximum < 180.";
                return true;
            }

            if (MinimumDistance <= 0f || MaximumDistance < MinimumDistance)
            {
                error = "Object distance must satisfy 0 < minimum <= maximum.";
                return true;
            }

            if (MaximumYaw < 0f || MaximumPitch < 0f || MaximumRoll < 0f)
            {
                error = "Rotation limits cannot be negative.";
                return true;
            }

            if (MinimumCenterSeparation < 0f || MinimumCenterSeparation > 0.8f)
            {
                error = "Minimum center separation must be between 0 and 0.8.";
                return true;
            }

            if (MinimumBoxPixels <= 0f || MinimumBoxPixels > Math.Min(Width, Height))
            {
                error = "Minimum box size must be positive and fit inside the image.";
                return true;
            }

            if (MaximumCropFraction < 0f || MaximumCropFraction > 1f)
            {
                error = "Maximum crop fraction must be between 0 and 1.";
                return true;
            }

            if (MaximumProjectedBoxContainmentCoverage <= 0f ||
                MaximumProjectedBoxContainmentCoverage > 1f)
            {
                error = "Projected box containment coverage must be greater than 0 and at most 1.";
                return true;
            }

            if (MaximumFrameRetries <= 0)
            {
                error = "Maximum frame retries must be positive.";
                return true;
            }

            if (MaximumBackgroundCropOffset < 0f || MaximumBackgroundCropOffset > 0.5f)
            {
                error = "Maximum background crop offset must be between 0 and 0.5.";
                return true;
            }

            if (MinimumBackgroundScale < 1f ||
                MaximumBackgroundScale < MinimumBackgroundScale)
            {
                error = "Background scale must satisfy 1 <= minimum <= maximum.";
                return true;
            }

            if (MinimumBackgroundBrightness <= 0f ||
                MaximumBackgroundBrightness < MinimumBackgroundBrightness)
            {
                error = "Background brightness range is invalid.";
                return true;
            }

            if (MinimumBackgroundContrast <= 0f ||
                MaximumBackgroundContrast < MinimumBackgroundContrast)
            {
                error = "Background contrast range is invalid.";
                return true;
            }

            if (MaximumBackgroundBlur < 0f)
            {
                error = "Maximum background blur cannot be negative.";
                return true;
            }

            if (MinimumBackgroundTemperature <= 0f ||
                MaximumBackgroundTemperature < MinimumBackgroundTemperature)
            {
                error = "Background temperature range is invalid.";
                return true;
            }

            error = null;
            return false;
        }

        private void OnValidate()
        {
            Debug.Assert(!TryGetValidationError(out var error), error);
        }

        private bool AllFloatsAreFinite()
        {
            var values = new[]
            {
                MinimumFov,
                MaximumFov,
                MinimumDistance,
                MaximumDistance,
                MaximumYaw,
                MaximumPitch,
                MaximumRoll,
                MinimumCenterSeparation,
                MinimumBoxPixels,
                MaximumCropFraction,
                MaximumProjectedBoxContainmentCoverage,
                MaximumBackgroundCropOffset,
                MinimumBackgroundScale,
                MaximumBackgroundScale,
                MinimumBackgroundBrightness,
                MaximumBackgroundBrightness,
                MinimumBackgroundContrast,
                MaximumBackgroundContrast,
                MaximumBackgroundBlur,
                MinimumBackgroundTemperature,
                MaximumBackgroundTemperature
            };

            foreach (var value in values)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
