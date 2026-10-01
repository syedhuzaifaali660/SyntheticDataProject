using System;
using System.Collections.Generic;
using UnityEngine;

namespace SyntheticData.Generation
{
    public sealed class FrameRecipeSampler
    {
        private const int ClassCount = 3;
        private const uint ClassStream = 0x4f1bbcdcu;
        private const uint AttemptStream = 0xa5cb9243u;
        private readonly GenerationConfig config;
        private readonly string configSignature;
        private readonly List<int> cumulativeObjectCounts = new List<int> { 0 };

        public FrameRecipeSampler(GenerationConfig config)
        {
            this.config = config != null
                ? config
                : throw new ArgumentNullException(nameof(config));
            config.ValidateOrThrow();
            configSignature = JsonUtility.ToJson(config);
        }

        public FrameRecipe Sample(int frameIndex)
        {
            return Sample(frameIndex, 0);
        }

        public FrameRecipe Sample(int frameIndex, int attemptIndex)
        {
            EnsureConfigHasNotChanged();

            if (frameIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frameIndex),
                    "Frame index cannot be negative.");
            }

            if (attemptIndex < 0 || attemptIndex >= config.MaximumFrameRetries)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(attemptIndex),
                    $"Attempt index must be between 0 and {config.MaximumFrameRetries - 1}.");
            }

            var outputSeed = DeriveSeed(config.Seed, frameIndex, 0u);
            var attemptSeed = DeriveSeed(outputSeed, attemptIndex, AttemptStream);
            var random = new SeededRandomSource(attemptSeed);
            // Keep the requested distribution fixed while retries change pose/appearance.
            var strataRandom = config.UseMixedDeviceProfiles
                ? new SeededRandomSource(DeriveSeed(outputSeed, 0, ClassStream)) : random;
            var objectCount = GetObjectCount(frameIndex);
            var captureProfile = SampleCaptureProfile(strataRandom);
            var outputResolution = captureProfile.Resolutions[strataRandom.Range(0, captureProfile.Resolutions.Length)];
            var lightBand = SampleLightingBand(strataRandom);
            var recipe = new FrameRecipe
            {
                FrameIndex = frameIndex,
                AttemptIndex = attemptIndex,
                Seed = attemptSeed,
                CaptureProfile = captureProfile,
                OutputWidth = outputResolution.Width,
                OutputHeight = outputResolution.Height,
                Background = SampleBackground(random),
                CameraFov = random.Range(config.MinimumFov, config.MaximumFov),
                Light = SampleLight(random, lightBand)
            };

            var firstObjectOrdinal = GetFirstObjectOrdinal(frameIndex);
            var centers = SampleSeparatedCenters(random, objectCount, captureProfile);
            for (var objectIndex = 0; objectIndex < objectCount; objectIndex++)
            {
                var sizeBand = config.UseMixedDeviceProfiles ? SampleObjectSizeBand(strataRandom) : ObjectSizeBand.Medium;
                var objectScale = !config.UseMixedDeviceProfiles
                    ? random.Range(0.85f, 1.15f)
                    : sizeBand == ObjectSizeBand.Small
                        ? random.Range(0.5f, 0.9f)
                        : sizeBand == ObjectSizeBand.Large
                            ? random.Range(1.1f, 1.6f)
                            : random.Range(0.85f, 1.15f);
                var objectDistance = !config.UseMixedDeviceProfiles
                    ? random.Range(config.MinimumDistance, config.MaximumDistance)
                    : sizeBand == ObjectSizeBand.Small
                        ? random.Range(Mathf.Max(config.MinimumDistance, 4f), config.MaximumDistance)
                        : sizeBand == ObjectSizeBand.Large
                            ? random.Range(config.MinimumDistance, Mathf.Min(config.MaximumDistance, 3f))
                            : random.Range(config.MinimumDistance, config.MaximumDistance);
                recipe.Objects.Add(new ObjectRecipe
                {
                    ClassId = SampleClassId(firstObjectOrdinal + objectIndex),
                    Center = centers[objectIndex],
                    Distance = objectDistance,
                    Scale = objectScale,
                    SizeBand = sizeBand,
                    EulerAngles = new Vector3(
                        random.Range(-config.MaximumPitch, config.MaximumPitch),
                        random.Range(-config.MaximumYaw, config.MaximumYaw),
                        random.Range(-config.MaximumRoll, config.MaximumRoll))
                });
            }

            return recipe;
        }

        private BackgroundRecipe SampleBackground(IRandomSource random)
        {
            return new BackgroundRecipe
            {
                Index = random.Range(0, config.BackgroundCount),
                CropOffset = new Vector2(
                    random.Range(
                        -config.MaximumBackgroundCropOffset,
                        config.MaximumBackgroundCropOffset),
                    random.Range(
                        -config.MaximumBackgroundCropOffset,
                        config.MaximumBackgroundCropOffset)),
                Scale = random.Range(
                    config.MinimumBackgroundScale,
                    config.MaximumBackgroundScale),
                Brightness = random.Range(
                    config.MinimumBackgroundBrightness,
                    config.MaximumBackgroundBrightness),
                Contrast = random.Range(
                    config.MinimumBackgroundContrast,
                    config.MaximumBackgroundContrast),
                Blur = random.Range(0f, config.MaximumBackgroundBlur),
                Temperature = random.Range(
                    config.MinimumBackgroundTemperature,
                    config.MaximumBackgroundTemperature)
            };
        }

        private LightRecipe SampleLight(IRandomSource random, LightingBand band)
        {
            var azimuth = random.Range(-180f, 180f) * Mathf.Deg2Rad;
            var elevation = random.Range(20f, 70f) * Mathf.Deg2Rad;
            var distance = random.Range(2f, 5f);
            var horizontalDistance = Mathf.Cos(elevation) * distance;
            var ambientMin = 0.6f;
            var ambientMax = 1.2f;
            var pointMin = 0.5f;
            var pointMax = 2f;
            if (config.UseMixedDeviceProfiles)
            {
                ambientMin = .45f;
                ambientMax = 1.1f;
                switch (band)
                {
                    case LightingBand.Low:
                        ambientMin = 0.15f;
                        ambientMax = 0.45f;
                        pointMin = 0.15f;
                        pointMax = 0.8f;
                        break;
                    case LightingBand.Bright:
                        ambientMin = 0.9f;
                        ambientMax = 1.5f;
                        pointMin = 1.5f;
                        pointMax = 3.5f;
                        break;
                }
            }
            return new LightRecipe
            {
                AmbientIntensity = random.Range(ambientMin, ambientMax),
                PointIntensity = random.Range(pointMin, pointMax),
                PointPosition = new Vector3(
                    Mathf.Sin(azimuth) * horizontalDistance,
                    Mathf.Sin(elevation) * distance,
                    Mathf.Cos(azimuth) * horizontalDistance),
                Temperature = config.UseMixedDeviceProfiles ? random.Range(2800f, 8500f) : random.Range(4000f, 7000f),
                Band = band
            };
        }

        private CaptureProfile SampleCaptureProfile(IRandomSource random)
        {
            if (!config.UseMixedDeviceProfiles) return CaptureProfileCatalog.Baseline(config.Width, config.Height);
            var sample = random.Range(0f, 1f);
            var cumulativeWeight = 0f;
            foreach (var profile in CaptureProfileCatalog.All)
            {
                cumulativeWeight += profile.Weight;
                if (sample < cumulativeWeight) return profile;
            }
            return CaptureProfileCatalog.All[CaptureProfileCatalog.All.Count - 1];
        }

        private static ObjectSizeBand SampleObjectSizeBand(IRandomSource random)
        {
            var sample = random.Range(0f, 1f);
            return sample < 0.3f ? ObjectSizeBand.Small : sample < 0.8f ? ObjectSizeBand.Medium : ObjectSizeBand.Large;
        }

        private static LightingBand SampleLightingBand(IRandomSource random)
        {
            var sample = random.Range(0f, 1f);
            return sample < 0.2f ? LightingBand.Low : sample < 0.8f ? LightingBand.Normal : LightingBand.Bright;
        }

        private List<Vector2> SampleSeparatedCenters(
            IRandomSource random,
            int objectCount,
            CaptureProfile captureProfile)
        {
            var minimumX = captureProfile.HorizontalSafeMargin;
            var maximumX = 1f - minimumX;
            var minimumY = captureProfile.VerticalSafeMargin;
            var maximumY = 1f - minimumY;
            const int maximumAttempts = 64;
            for (var attempt = 0; attempt < maximumAttempts; attempt++)
            {
                var candidates = new List<Vector2>(objectCount);
                var layoutIsValid = true;
                for (var objectIndex = 0; objectIndex < objectCount; objectIndex++)
                {
                    var candidate = new Vector2(
                        random.Range(minimumX, maximumX),
                        random.Range(minimumY, maximumY));
                    if (!IsSeparated(candidate, candidates))
                    {
                        layoutIsValid = false;
                        break;
                    }

                    candidates.Add(candidate);
                }

                if (layoutIsValid)
                {
                    return candidates;
                }
            }

            var fallbackCenters = new[]
            {
                new Vector2(minimumX, minimumY),
                new Vector2(maximumX, minimumY),
                new Vector2(0.5f, maximumY)
            };
            Shuffle(fallbackCenters, random);
            var fallback = new List<Vector2>(objectCount);
            for (var index = 0; index < objectCount; index++)
            {
                if (!IsSeparated(fallbackCenters[index], fallback))
                {
                    throw new InvalidOperationException(
                        "No valid object-center layout exists for the configured separation.");
                }

                fallback.Add(fallbackCenters[index]);
            }

            return fallback;
        }

        private bool IsSeparated(Vector2 candidate, IReadOnlyList<Vector2> existingCenters)
        {
            for (var index = 0; index < existingCenters.Count; index++)
            {
                if (Vector2.Distance(candidate, existingCenters[index]) + 0.000001f <
                    config.MinimumCenterSeparation)
                {
                    return false;
                }
            }

            return true;
        }

        private int GetFirstObjectOrdinal(int frameIndex)
        {
            while (cumulativeObjectCounts.Count <= frameIndex)
            {
                var precedingFrameIndex = cumulativeObjectCounts.Count - 1;
                var frameObjectCount = GetObjectCount(precedingFrameIndex);
                cumulativeObjectCounts.Add(
                    cumulativeObjectCounts[precedingFrameIndex] + frameObjectCount);
            }

            return cumulativeObjectCounts[frameIndex];
        }

        private int GetObjectCount(int frameIndex)
        {
            var random = new SeededRandomSource(DeriveSeed(config.Seed, frameIndex, 0u));
            return random.Range(config.MinimumObjects, config.MaximumObjects + 1);
        }

        private int SampleClassId(int objectOrdinal)
        {
            var cycleIndex = objectOrdinal / ClassCount;
            var cycleOffset = objectOrdinal % ClassCount;
            var classIds = new[] { 0, 1, 2 };
            var random = new SeededRandomSource(
                DeriveSeed(config.Seed, cycleIndex, ClassStream));

            Shuffle(classIds, random);

            return classIds[cycleOffset];
        }

        private void EnsureConfigHasNotChanged()
        {
            if (JsonUtility.ToJson(config) != configSignature)
            {
                throw new InvalidOperationException(
                    "GenerationConfig changed after the sampler was constructed.");
            }
        }

        private static void Shuffle<T>(T[] items, IRandomSource random)
        {
            for (var index = items.Length - 1; index > 0; index--)
            {
                var swapIndex = random.Range(0, index + 1);
                (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
            }
        }

        private static int DeriveSeed(int globalSeed, int value, uint stream)
        {
            unchecked
            {
                var hash = (uint)globalSeed ^ stream;
                hash ^= (uint)value + 0x9e3779b9u + (hash << 6) + (hash >> 2);
                hash ^= hash >> 16;
                hash *= 0x7feb352du;
                hash ^= hash >> 15;
                hash *= 0x846ca68bu;
                hash ^= hash >> 16;
                return (int)hash;
            }
        }
    }
}
