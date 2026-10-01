using System.Linq;
using NUnit.Framework;
using SyntheticData.Generation;
using UnityEngine;

namespace SyntheticData.Tests
{
    public sealed class FrameRecipeSamplerTests
    {
        [Test]
        public void RetryPreservesMixedDeviceStrata()
        {
            var config = GenerationConfig.CreateMixedDeviceDefault(42);
            var sampler = new FrameRecipeSampler(config);
            try
            {
                for (var frame = 0; frame < 200; frame++)
                {
                    var first = sampler.Sample(frame, 0);
                    var retry = sampler.Sample(frame, 19);
                    Assert.That(retry.CaptureProfile.Name, Is.EqualTo(first.CaptureProfile.Name));
                    Assert.That(retry.OutputWidth, Is.EqualTo(first.OutputWidth));
                    Assert.That(retry.OutputHeight, Is.EqualTo(first.OutputHeight));
                    Assert.That(retry.Light.Band, Is.EqualTo(first.Light.Band));
                    Assert.That(retry.Objects.Select(o => o.SizeBand), Is.EqualTo(first.Objects.Select(o => o.SizeBand)));
                }
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void SameSeedAndFrameProduceSameRecipe()
        {
            var config = GenerationConfig.CreateTestDefault(seed: 42);
            var first = new FrameRecipeSampler(config).Sample(7);
            var second = new FrameRecipeSampler(config).Sample(7);

            Assert.That(second.ToJson(), Is.EqualTo(first.ToJson()));
        }

        [Test]
        public void MixedDeviceProfileUsesAnApprovedNativeResolution()
        {
            var config = GenerationConfig.CreateMixedDeviceDefault(seed: 42);
            var recipe = new FrameRecipeSampler(config).Sample(7);

            Assert.That(recipe.CaptureProfile, Is.Not.Null);
            Assert.That(
                CaptureProfileCatalog.AllowedProfileNames,
                Does.Contain(recipe.CaptureProfile.Name));
            Assert.That(
                recipe.CaptureProfile.ContainsResolution(recipe.OutputWidth, recipe.OutputHeight),
                Is.True);
            Assert.That(recipe.OutputWidth, Is.GreaterThan(0));
            Assert.That(recipe.OutputHeight, Is.GreaterThan(0));
        }

        [Test]
        public void MixedDeviceRecipeAssignsValidLightingAndObjectSizeBands()
        {
            var config = GenerationConfig.CreateMixedDeviceDefault(seed: 42);
            config.MinimumObjects = 1;
            var recipe = new FrameRecipeSampler(config).Sample(7);

            Assert.That(recipe.Light.Band, Is.EqualTo(LightingBand.Low)
                .Or.EqualTo(LightingBand.Normal)
                .Or.EqualTo(LightingBand.Bright));
            Assert.That(recipe.Objects, Is.All.Matches<ObjectRecipe>(item =>
                item.SizeBand == ObjectSizeBand.Small ||
                item.SizeBand == ObjectSizeBand.Medium ||
                item.SizeBand == ObjectSizeBand.Large));
        }

        [Test]
        public void MixedDeviceSamplingUsesDistinctLightingAndScaleRanges()
        {
            var config = GenerationConfig.CreateMixedDeviceDefault(seed: 42);
            config.MinimumObjects = 1;
            config.MaximumObjects = 1;
            var sampler = new FrameRecipeSampler(config);
            var lightingCounts = new int[3];
            var sizeCounts = new int[3];

            for (var frameIndex = 0; frameIndex < 1000; frameIndex++)
            {
                var recipe = sampler.Sample(frameIndex);
                lightingCounts[(int)recipe.Light.Band]++;
                var item = recipe.Objects[0];
                sizeCounts[(int)item.SizeBand]++;
                if (recipe.Light.Band == LightingBand.Low)
                    Assert.That(recipe.Light.AmbientIntensity, Is.InRange(0.15f, 0.45f));
                else if (recipe.Light.Band == LightingBand.Bright)
                    Assert.That(recipe.Light.AmbientIntensity, Is.InRange(0.9f, 1.5f));
                if (item.SizeBand == ObjectSizeBand.Small)
                    Assert.That(item.Scale, Is.InRange(0.5f, 0.9f));
                else if (item.SizeBand == ObjectSizeBand.Large)
                    Assert.That(item.Scale, Is.InRange(1.1f, 1.6f));
            }

            Assert.That(lightingCounts[0], Is.InRange(150, 250));
            Assert.That(lightingCounts[1], Is.InRange(500, 700));
            Assert.That(lightingCounts[2], Is.InRange(150, 250));
            Assert.That(sizeCounts[0], Is.InRange(250, 350));
            Assert.That(sizeCounts[1], Is.InRange(400, 600));
            Assert.That(sizeCounts[2], Is.InRange(150, 250));
        }

        [Test]
        public void RecipeContainsAtMostThreeObjectsWithValidClassIds()
        {
            var recipe = new FrameRecipeSampler(
                GenerationConfig.CreateTestDefault(seed: 42)).Sample(11);

            Assert.That(recipe.Objects.Count, Is.InRange(0, 3));
            Assert.That(recipe.Objects, Is.All.Matches<ObjectRecipe>(
                item => item.ClassId >= 0 && item.ClassId <= 2));
        }

        [Test]
        public void SamplingIsIndependentOfCallOrderAndUnityRandomState()
        {
            UnityEngine.Random.InitState(9137);
            var unityRandomState = UnityEngine.Random.state;
            var config = GenerationConfig.CreateTestDefault(seed: 42);
            var sampler = new FrameRecipeSampler(config);

            sampler.Sample(200);
            var afterOutOfOrderSample = sampler.Sample(7);
            var directSample = new FrameRecipeSampler(config).Sample(7);

            Assert.That(afterOutOfOrderSample.ToJson(), Is.EqualTo(directSample.ToJson()));
            Assert.That(UnityEngine.Random.state, Is.EqualTo(unityRandomState));
        }

        [Test]
        public void SampledValuesStayInsideConfiguredRanges()
        {
            var config = GenerationConfig.CreateTestDefault(seed: 42);
            var sampler = new FrameRecipeSampler(config);

            for (var frameIndex = 0; frameIndex < 100; frameIndex++)
            {
                var recipe = sampler.Sample(frameIndex);
                Assert.That(recipe.FrameIndex, Is.EqualTo(frameIndex));
                Assert.That(recipe.Background.Index, Is.InRange(0, config.BackgroundCount - 1));
                Assert.That(recipe.CameraFov, Is.InRange(config.MinimumFov, config.MaximumFov));
                Assert.That(recipe.Light.AmbientIntensity, Is.InRange(0.6f, 1.2f));
                Assert.That(recipe.Light.PointIntensity, Is.InRange(0.5f, 2f));
                Assert.That(recipe.Light.Temperature, Is.InRange(4000f, 7000f));

                foreach (var item in recipe.Objects)
                {
                    Assert.That(item.Center.x, Is.InRange(0.1f, 0.9f));
                    Assert.That(item.Center.y, Is.InRange(0.1f, 0.9f));
                    Assert.That(item.Distance, Is.InRange(
                        config.MinimumDistance,
                        config.MaximumDistance));
                    Assert.That(item.Scale, Is.InRange(0.85f, 1.15f));
                    Assert.That(Mathf.Abs(item.EulerAngles.x), Is.LessThanOrEqualTo(
                        config.MaximumPitch));
                    Assert.That(Mathf.Abs(item.EulerAngles.y), Is.LessThanOrEqualTo(
                        config.MaximumYaw));
                    Assert.That(Mathf.Abs(item.EulerAngles.z), Is.LessThanOrEqualTo(
                        config.MaximumRoll));
                }
            }
        }

        [Test]
        public void ObjectCentersRespectMinimumSeparation()
        {
            var config = GenerationConfig.CreateTestDefault(seed: 42);
            config.MinimumObjects = 3;
            config.MaximumObjects = 3;
            var sampler = new FrameRecipeSampler(config);

            for (var frameIndex = 0; frameIndex < 100; frameIndex++)
            {
                var objects = sampler.Sample(frameIndex).Objects;
                for (var first = 0; first < objects.Count; first++)
                {
                    for (var second = first + 1; second < objects.Count; second++)
                    {
                        Assert.That(
                            Vector2.Distance(objects[first].Center, objects[second].Center),
                            Is.GreaterThanOrEqualTo(config.MinimumCenterSeparation));
                    }
                }
            }
        }

        [Test]
        public void MaximumSupportedSeparationUsesAValidDeterministicLayout()
        {
            var config = GenerationConfig.CreateTestDefault(seed: 42);
            config.MinimumObjects = 3;
            config.MaximumObjects = 3;
            config.MinimumCenterSeparation = 0.8f;
            var sampler = new FrameRecipeSampler(config);

            for (var frameIndex = 0; frameIndex < 20; frameIndex++)
            {
                var objects = sampler.Sample(frameIndex).Objects;
                for (var first = 0; first < objects.Count; first++)
                {
                    for (var second = first + 1; second < objects.Count; second++)
                    {
                        Assert.That(
                            Vector2.Distance(objects[first].Center, objects[second].Center),
                            Is.GreaterThanOrEqualTo(0.8f - 0.00001f));
                    }
                }
            }
        }

        [Test]
        public void RoundRobinClassesRemainBalancedAcrossFrames()
        {
            var config = GenerationConfig.CreateTestDefault(seed: 42);
            var sampler = new FrameRecipeSampler(config);
            var counts = new int[3];

            for (var frameIndex = 0; frameIndex < 200; frameIndex++)
            {
                foreach (var item in sampler.Sample(frameIndex).Objects)
                {
                    counts[item.ClassId]++;
                }
            }

            Assert.That(Mathf.Max(counts), Is.LessThanOrEqualTo(Mathf.Min(counts) + 1));
        }

        [Test]
        public void InvalidConfigurationIsRejectedBeforeSampling()
        {
            var config = GenerationConfig.CreateTestDefault(seed: 42);
            config.Width = 0;

            Assert.That(
                () => new FrameRecipeSampler(config),
                Throws.InvalidOperationException.With.Message.Contains("dimensions"));
        }

        [Test]
        public void NonFiniteAndCrossFieldConfigurationIsRejected()
        {
            var nonFinite = GenerationConfig.CreateTestDefault(seed: 42);
            nonFinite.MaximumYaw = float.NaN;
            Assert.That(
                () => new FrameRecipeSampler(nonFinite),
                Throws.InvalidOperationException.With.Message.Contains("finite"));

            var impossibleBox = GenerationConfig.CreateTestDefault(seed: 42);
            impossibleBox.MinimumBoxPixels = impossibleBox.Width + 1f;
            Assert.That(
                () => new FrameRecipeSampler(impossibleBox),
                Throws.InvalidOperationException.With.Message.Contains("image"));

            var invalidContainmentThreshold = GenerationConfig.CreateTestDefault(seed: 42);
            invalidContainmentThreshold.MaximumProjectedBoxContainmentCoverage = 0f;
            Assert.That(
                () => new FrameRecipeSampler(invalidContainmentThreshold),
                Throws.InvalidOperationException.With.Message.Contains("containment"));
        }

        [Test]
        public void MixedDeviceDefaultSerializesProjectedContainmentThreshold()
        {
            var config = GenerationConfig.CreateMixedDeviceDefault(seed: 42);

            Assert.That(config.MaximumProjectedBoxContainmentCoverage, Is.EqualTo(0.95f));
            Assert.That(
                JsonUtility.ToJson(config),
                Does.Contain("\"MaximumProjectedBoxContainmentCoverage\":"));
        }

        [Test]
        public void RetryChangesSceneRandomnessButPreservesOutputIdentityAndClassSchedule()
        {
            var config = GenerationConfig.CreateTestDefault(seed: 42);
            config.MinimumObjects = 3;
            config.MaximumObjects = 3;
            var sampler = new FrameRecipeSampler(config);

            var firstAttempt = sampler.Sample(frameIndex: 7, attemptIndex: 0);
            var retry = sampler.Sample(frameIndex: 7, attemptIndex: 1);

            Assert.That(firstAttempt.FrameIndex, Is.EqualTo(retry.FrameIndex));
            Assert.That(firstAttempt.AttemptIndex, Is.EqualTo(0));
            Assert.That(retry.AttemptIndex, Is.EqualTo(1));
            Assert.That(firstAttempt.Seed, Is.Not.EqualTo(retry.Seed));
            Assert.That(retry.ToJson(), Is.Not.EqualTo(firstAttempt.ToJson()));
            Assert.That(
                retry.Objects.Select(item => item.ClassId),
                Is.EqualTo(firstAttempt.Objects.Select(item => item.ClassId)));
        }

        [Test]
        public void RecipeContainsAllBackgroundAugmentationValues()
        {
            var config = GenerationConfig.CreateTestDefault(seed: 42);
            var recipe = new FrameRecipeSampler(config).Sample(7);

            Assert.That(recipe.Background.Index, Is.InRange(0, config.BackgroundCount - 1));
            Assert.That(recipe.Background.CropOffset.x, Is.InRange(
                -config.MaximumBackgroundCropOffset,
                config.MaximumBackgroundCropOffset));
            Assert.That(recipe.Background.CropOffset.y, Is.InRange(
                -config.MaximumBackgroundCropOffset,
                config.MaximumBackgroundCropOffset));
            Assert.That(recipe.Background.Scale, Is.InRange(
                config.MinimumBackgroundScale,
                config.MaximumBackgroundScale));
            Assert.That(recipe.Background.Brightness, Is.InRange(
                config.MinimumBackgroundBrightness,
                config.MaximumBackgroundBrightness));
            Assert.That(recipe.Background.Contrast, Is.InRange(
                config.MinimumBackgroundContrast,
                config.MaximumBackgroundContrast));
            Assert.That(recipe.Background.Blur, Is.InRange(0f, config.MaximumBackgroundBlur));
            Assert.That(recipe.Background.Temperature, Is.InRange(
                config.MinimumBackgroundTemperature,
                config.MaximumBackgroundTemperature));
        }

        [Test]
        public void SamplerRejectsConfigurationMutationAfterConstruction()
        {
            var config = GenerationConfig.CreateTestDefault(seed: 42);
            var sampler = new FrameRecipeSampler(config);
            config.Seed = 99;

            Assert.That(
                () => sampler.Sample(0),
                Throws.InvalidOperationException.With.Message.Contains("changed"));
        }

        [Test]
        public void TestDefaultMatchesTheDocumentedBaseline()
        {
            var config = GenerationConfig.CreateTestDefault(seed: 42);

            Assert.That(config.Seed, Is.EqualTo(42));
            Assert.That(config.Width, Is.EqualTo(640));
            Assert.That(config.Height, Is.EqualTo(640));
            Assert.That(config.BackgroundCount, Is.EqualTo(98));
            Assert.That(config.MinimumObjects, Is.EqualTo(0));
            Assert.That(config.MaximumObjects, Is.EqualTo(3));
            Assert.That(config.MinimumFov, Is.EqualTo(40f));
            Assert.That(config.MaximumFov, Is.EqualTo(75f));
            Assert.That(config.MinimumDistance, Is.EqualTo(1.5f));
            Assert.That(config.MaximumDistance, Is.EqualTo(5f));
            Assert.That(config.MaximumYaw, Is.EqualTo(35f));
            Assert.That(config.MaximumPitch, Is.EqualTo(20f));
            Assert.That(config.MaximumRoll, Is.EqualTo(12f));
            Assert.That(config.MinimumCenterSeparation, Is.EqualTo(0.18f));
            Assert.That(config.MinimumBoxPixels, Is.EqualTo(24f));
            Assert.That(config.MaximumCropFraction, Is.EqualTo(0.25f));
            Assert.That(config.MaximumProjectedBoxContainmentCoverage, Is.EqualTo(1f));
            Assert.That(config.MaximumFrameRetries, Is.EqualTo(20));
            Assert.That(config.MaximumBackgroundCropOffset, Is.EqualTo(0.1f));
            Assert.That(config.MinimumBackgroundScale, Is.EqualTo(1f));
            Assert.That(config.MaximumBackgroundScale, Is.EqualTo(1.3f));
            Assert.That(config.MinimumBackgroundBrightness, Is.EqualTo(0.8f));
            Assert.That(config.MaximumBackgroundBrightness, Is.EqualTo(1.2f));
            Assert.That(config.MinimumBackgroundContrast, Is.EqualTo(0.85f));
            Assert.That(config.MaximumBackgroundContrast, Is.EqualTo(1.15f));
            Assert.That(config.MaximumBackgroundBlur, Is.EqualTo(1f));
            Assert.That(config.MinimumBackgroundTemperature, Is.EqualTo(4500f));
            Assert.That(config.MaximumBackgroundTemperature, Is.EqualTo(7500f));
        }
    }
}
