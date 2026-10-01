using System;
using System.Collections;
using System.Collections.Generic;
using SyntheticData.Domain;
using SyntheticData.Generation;
using SyntheticData.Labels;
using SyntheticData.Output;
using SyntheticData.Scene;
using UnityEngine;

namespace SyntheticData.Capture
{
    public interface IFrameRecipeSampler
    {
        FrameRecipe Sample(int frameIndex, int attemptIndex);
    }

    public interface IGeneratedFrame
    {
        IReadOnlyList<GameObject> Objects { get; }
    }

    public interface IFrameSceneBuilder
    {
        IGeneratedFrame Build(FrameRecipe recipe);
        void Clear(IGeneratedFrame frame);
    }

    public interface IFrameArtifactWriter
    {
        void Write(FrameArtifact artifact);
    }

    public sealed class FrameRejection
    {
        public FrameRejection(int frameIndex, int attemptIndex, int seed, string reason)
        {
            FrameIndex = frameIndex;
            AttemptIndex = attemptIndex;
            Seed = seed;
            Reason = reason;
        }

        public int FrameIndex { get; }
        public int AttemptIndex { get; }
        public int Seed { get; }
        public string Reason { get; }
    }

    public sealed class SyntheticCaptureController
    {
        private readonly Camera camera;
        private readonly GenerationConfig config;
        private readonly IFrameRecipeSampler sampler;
        private readonly IFrameSceneBuilder sceneBuilder;
        private readonly IBoundsProjector projector;
        private readonly ICameraCaptureService captureService;
        private readonly IFrameArtifactWriter writer;
        private readonly BackgroundCatalog backgrounds;
        private readonly Action<IReadOnlyList<YoloBox>> previewPublisher;
        private readonly Action previewClearer;
        private readonly List<FrameRejection> rejections = new List<FrameRejection>();

        public SyntheticCaptureController(
            Camera camera,
            GenerationConfig config,
            IFrameRecipeSampler sampler,
            IFrameSceneBuilder sceneBuilder,
            IBoundsProjector projector,
            ICameraCaptureService captureService,
            IFrameArtifactWriter writer,
            BackgroundCatalog backgrounds,
            Action<IReadOnlyList<YoloBox>> previewPublisher = null,
            Action previewClearer = null)
        {
            this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            this.sceneBuilder = sceneBuilder ?? throw new ArgumentNullException(nameof(sceneBuilder));
            this.projector = projector ?? throw new ArgumentNullException(nameof(projector));
            this.captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            this.backgrounds = backgrounds ?? throw new ArgumentNullException(nameof(backgrounds));
            this.previewPublisher = previewPublisher;
            this.previewClearer = previewClearer;
            config.ValidateOrThrow();
            backgrounds.Validate();
        }

        public SyntheticCaptureController(
            Camera camera,
            GenerationConfig config,
            FrameRecipeSampler sampler,
            FrameSceneBuilder sceneBuilder,
            IBoundsProjector projector,
            ICameraCaptureService captureService,
            AtomicCaptureWriter writer,
            BackgroundCatalog backgrounds,
            Action<IReadOnlyList<YoloBox>> previewPublisher = null,
            Action previewClearer = null)
            : this(
                camera,
                config,
                new RecipeSamplerAdapter(sampler),
                new SceneBuilderAdapter(sceneBuilder),
                projector,
                captureService,
                new ArtifactWriterAdapter(writer),
                backgrounds,
                previewPublisher,
                previewClearer)
        {
        }

        public IReadOnlyList<FrameRejection> Rejections => rejections;

        public IEnumerator GenerateRun(string runId, int frameCount)
        {
            if (string.IsNullOrWhiteSpace(runId))
            {
                throw new ArgumentException("A run ID is required.", nameof(runId));
            }

            if (frameCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameCount), "Frame count cannot be negative.");
            }

            rejections.Clear();
            var runConfigJson = JsonUtility.ToJson(config);
            for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                var accepted = false;
                FrameRejection lastRejection = null;
                for (var attemptIndex = 0; attemptIndex < config.MaximumFrameRetries; attemptIndex++)
                {
                    var recipe = sampler.Sample(frameIndex, attemptIndex);
                    if (recipe == null)
                    {
                        throw new InvalidOperationException(
                            $"Recipe sampler returned null for frame {frameIndex + 1}, attempt {attemptIndex}.");
                    }

                    if (recipe.FrameIndex != frameIndex || recipe.AttemptIndex != attemptIndex)
                    {
                        throw new InvalidOperationException(
                            $"Recipe identity mismatch for frame {frameIndex + 1}, attempt {attemptIndex}. " +
                            "The sampler must preserve the frame index and attempt index.");
                    }

                    IGeneratedFrame generatedFrame = null;
                    try
                    {
                        previewClearer?.Invoke();
                        generatedFrame = sceneBuilder.Build(recipe);
                        if (generatedFrame == null)
                        {
                            throw new InvalidOperationException(
                                $"Scene builder returned null for frame {frameIndex + 1}, attempt {attemptIndex}.");
                        }

                        yield return new WaitForEndOfFrame();

                        if (!TryCreateArtifact(
                                runId,
                                runConfigJson,
                                recipe,
                                generatedFrame,
                                out var artifact,
                                out var rejectionReason))
                        {
                            lastRejection = new FrameRejection(
                                frameIndex,
                                attemptIndex,
                                recipe.Seed,
                                rejectionReason);
                            rejections.Add(lastRejection);
                            continue;
                        }

                        previewPublisher?.Invoke(artifact.Boxes);
                        writer.Write(artifact);
                        accepted = true;
                        if (previewPublisher != null)
                        {
                            // The capture is already encoded. Keep the generated objects and
                            // editor-only overlay together for one visible Game-view frame.
                            yield return null;
                        }

                        previewClearer?.Invoke();
                    }
                    finally
                    {
                        if (generatedFrame != null)
                        {
                            sceneBuilder.Clear(generatedFrame);
                        }
                    }

                    if (accepted)
                    {
                        break;
                    }
                }

                if (!accepted)
                {
                    var detail = lastRejection == null
                        ? "No rejection record was produced."
                        : $"Last rejection: {lastRejection.Reason} (seed {lastRejection.Seed}, " +
                          $"attempt {lastRejection.AttemptIndex}).";
                    throw new InvalidOperationException(
                        $"MaximumFrameRetries ({config.MaximumFrameRetries}) exhausted for frame {frameIndex + 1}. " +
                        detail);
                }
            }
        }

        private bool TryCreateArtifact(
            string runId,
            string runConfigJson,
            FrameRecipe recipe,
            IGeneratedFrame generatedFrame,
            out FrameArtifact artifact,
            out string rejectionReason)
        {
            artifact = null;
            rejectionReason = null;
            if (generatedFrame.Objects == null)
            {
                rejectionReason = "Generated frame returned a null object list.";
                return false;
            }

            if (generatedFrame.Objects.Count != recipe.Objects.Count)
            {
                rejectionReason =
                    $"Generated object count {generatedFrame.Objects.Count} does not match recipe count " +
                    $"{recipe.Objects.Count}.";
                return false;
            }

            var boxes = new List<YoloBox>(recipe.Objects.Count);
            var projectedBounds = new List<PixelBounds>(recipe.Objects.Count);
            var manifestObjects = new List<ManifestObject>(recipe.Objects.Count);
            var outputWidth = recipe.OutputWidth > 0 ? recipe.OutputWidth : config.Width;
            var outputHeight = recipe.OutputHeight > 0 ? recipe.OutputHeight : config.Height;
            var letterbox = LetterboxTransform.Create(outputWidth, outputHeight, 640);
            for (var index = 0; index < recipe.Objects.Count; index++)
            {
                var target = generatedFrame.Objects[index];
                var objectRecipe = recipe.Objects[index];
                if (!projector.TryProject(
                        camera,
                        target,
                        outputWidth,
                        outputHeight,
                        out var bounds))
                {
                    var profileName = recipe.CaptureProfile == null
                        ? "Baseline"
                        : recipe.CaptureProfile.Name;
                    rejectionReason =
                        $"Object {index} failed screen-space projection " +
                        $"(class {objectRecipe.ClassId}, center {objectRecipe.Center}, " +
                        $"distance {objectRecipe.Distance:0.00}, scale {objectRecipe.Scale:0.00}, " +
                        $"profile {profileName}).";
                    return false;
                }

                var box = bounds.ToYolo(objectRecipe.ClassId, outputWidth, outputHeight);
                boxes.Add(box);
                var sourceRect = new Rect(
                    box.CenterX - box.Width * 0.5f,
                    1f - box.CenterY - box.Height * 0.5f,
                    box.Width,
                    box.Height);
                var modelRect = letterbox.SourceToModel(sourceRect);
                var modelBoxSide = Mathf.Max(modelRect.width, modelRect.height) * 640f;
                if (config.UseMixedDeviceProfiles && !IsWithinSizeBand(objectRecipe.SizeBand, modelBoxSide))
                {
                    rejectionReason = $"Object {index} projected side {modelBoxSide:0.0}px does not match " +
                                      $"size band {objectRecipe.SizeBand}.";
                    return false;
                }

                if (config.UseMixedDeviceProfiles)
                {
                    for (var priorIndex = 0; priorIndex < projectedBounds.Count; priorIndex++)
                    {
                        var containmentCoverage = GetSmallerBoxIntersectionCoverage(
                            projectedBounds[priorIndex],
                            bounds);
                        if (containmentCoverage >= config.MaximumProjectedBoxContainmentCoverage)
                        {
                            rejectionReason =
                                $"Projected boxes for objects {priorIndex} and {index} overlap " +
                                $"{containmentCoverage:0.000} of the smaller box, meeting the " +
                                $"mixed-device containment threshold " +
                                $"{config.MaximumProjectedBoxContainmentCoverage:0.000}.";
                            return false;
                        }
                    }
                }

                projectedBounds.Add(bounds);
                manifestObjects.Add(new ManifestObject(
                    box,
                    GetClassName(objectRecipe.ClassId, target),
                    objectRecipe.Center,
                    objectRecipe.Distance,
                    objectRecipe.Scale,
                    objectRecipe.EulerAngles,
                    objectRecipe.SizeBand.ToString(),
                    modelRect.width * 640f,
                    modelRect.height * 640f));
            }

            var frameId = recipe.FrameIndex + 1;
            var manifest = new ManifestRecord(
                runId,
                frameId,
                recipe.Seed,
                "unassigned",
                backgrounds.GetId(recipe.Background.Index),
                outputWidth,
                outputHeight,
                manifestObjects,
                recipe.CaptureProfile == null ? null : recipe.CaptureProfile.Name,
                letterbox.Scale,
                letterbox.PaddingX,
                letterbox.PaddingY,
                recipe.Light.Band.ToString());
            var imageBytes = captureService.CapturePng(camera, outputWidth, outputHeight);
            artifact = new FrameArtifact(frameId, imageBytes, boxes, manifest, runConfigJson);
            return true;
        }

        private static float GetSmallerBoxIntersectionCoverage(PixelBounds first, PixelBounds second)
        {
            var intersectionWidth = Mathf.Max(
                0f,
                Mathf.Min(first.MaxX, second.MaxX) - Mathf.Max(first.MinX, second.MinX));
            var intersectionHeight = Mathf.Max(
                0f,
                Mathf.Min(first.MaxY, second.MaxY) - Mathf.Max(first.MinY, second.MinY));
            var smallerArea = Mathf.Min(first.Width * first.Height, second.Width * second.Height);
            return smallerArea > 0f
                ? intersectionWidth * intersectionHeight / smallerArea
                : 0f;
        }

        private static bool IsWithinSizeBand(ObjectSizeBand band, float longestSide)
        {
            switch (band)
            {
                case ObjectSizeBand.Small:
                    // Allow projection/rounding tolerance at the small/medium boundary.
                    // Keep small but still visible objects; sub-8px projections are unusable labels.
                    return longestSide >= 8f && longestSide <= 72f;
                case ObjectSizeBand.Medium:
                    return longestSide > 72f && longestSide <= 192f;
                case ObjectSizeBand.Large:
                    return longestSide > 192f && longestSide <= 448f;
                default:
                    return false;
            }
        }

        private static string GetClassName(int classId, GameObject target)
        {
            var label = target == null ? null : target.GetComponent<PokemonLabel>();
            if (label != null)
            {
                if (label.ClassId != classId)
                {
                    throw new InvalidOperationException(
                        $"Projected object label class {label.ClassId} does not match recipe class {classId}.");
                }

                return label.ClassName;
            }

            if (!Enum.IsDefined(typeof(PokemonClass), classId))
            {
                throw new InvalidOperationException($"Recipe contains invalid Pokemon class ID {classId}.");
            }

            return ((PokemonClass)classId).ToString().ToLowerInvariant();
        }

        private sealed class RecipeSamplerAdapter : IFrameRecipeSampler
        {
            private readonly FrameRecipeSampler sampler;

            public RecipeSamplerAdapter(FrameRecipeSampler sampler)
            {
                this.sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            }

            public FrameRecipe Sample(int frameIndex, int attemptIndex)
            {
                return sampler.Sample(frameIndex, attemptIndex);
            }
        }

        private sealed class SceneBuilderAdapter : IFrameSceneBuilder
        {
            private readonly FrameSceneBuilder builder;

            public SceneBuilderAdapter(FrameSceneBuilder builder)
            {
                this.builder = builder ?? throw new ArgumentNullException(nameof(builder));
            }

            public IGeneratedFrame Build(FrameRecipe recipe)
            {
                return new ConcreteGeneratedFrame(builder.Build(recipe));
            }

            public void Clear(IGeneratedFrame frame)
            {
                var concrete = frame as ConcreteGeneratedFrame;
                if (concrete == null)
                {
                    throw new ArgumentException("Frame was not created by this scene builder.", nameof(frame));
                }

                builder.Clear(concrete.Frame);
            }
        }

        private sealed class ConcreteGeneratedFrame : IGeneratedFrame
        {
            public ConcreteGeneratedFrame(SpawnedFrame frame)
            {
                Frame = frame ?? throw new ArgumentNullException(nameof(frame));
                var roots = new List<GameObject>(frame.SpawnedObjects.Count);
                foreach (var spawnedObject in frame.SpawnedObjects)
                {
                    roots.Add(spawnedObject.Root);
                }

                Objects = roots;
            }

            public SpawnedFrame Frame { get; }
            public IReadOnlyList<GameObject> Objects { get; }
        }

        private sealed class ArtifactWriterAdapter : IFrameArtifactWriter
        {
            private readonly AtomicCaptureWriter writer;

            public ArtifactWriterAdapter(AtomicCaptureWriter writer)
            {
                this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            }

            public void Write(FrameArtifact artifact)
            {
                writer.Write(artifact);
            }
        }
    }
}
