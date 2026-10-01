using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using SyntheticData.Capture;
using SyntheticData.Generation;
using SyntheticData.Labels;
using SyntheticData.Output;
using SyntheticData.Scene;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SyntheticData.Tests
{
    public sealed class SyntheticCaptureControllerTests
    {
        private readonly List<GameObject> generatedObjects = new List<GameObject>();
        private readonly List<GenerationConfig> generationConfigs = new List<GenerationConfig>();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Texture2D> textures = new List<Texture2D>();
        private readonly List<string> temporaryDirectories = new List<string>();

        [UnityTest]
        public IEnumerator RetriesInvalidProjectionAndRecordsSeedBeforeWriting()
        {
            var config = CreateConfig(2);
            var sampler = new FakeRecipeSampler(CreateRecipe(0, 11), CreateRecipe(0, 22));
            var builder = new FakeSceneBuilder(generatedObjects);
            var projector = new FakeProjector(false, true);
            var writer = new FakeWriter();
            var controller = CreateController(config, sampler, builder, projector, writer);

            yield return controller.GenerateRun("retry-run", 1);

            Assert.That(sampler.Attempts, Is.EqualTo(2));
            Assert.That(writer.Artifacts.Count, Is.EqualTo(1));
            Assert.That(controller.Rejections, Has.Count.EqualTo(1));
            Assert.That(controller.Rejections[0].FrameIndex, Is.EqualTo(0));
            Assert.That(controller.Rejections[0].Seed, Is.EqualTo(11));
            Assert.That(controller.Rejections[0].AttemptIndex, Is.EqualTo(0));
            Assert.That(controller.Rejections[0].Reason, Does.Contain("Object 0"));
            Assert.That(builder.ClearCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator FirstFrameUsesOneBasedIdAndPreservesManifestObjectMapping()
        {
            var config = CreateConfig(1);
            var recipe = CreateRecipe(0, 73);
            recipe.Objects[0].ClassId = 2;
            recipe.Objects[0].Center = new Vector2(0.2f, 0.7f);
            recipe.Objects[0].Distance = 3.5f;
            recipe.Objects[0].Scale = 1.1f;
            recipe.Objects[0].EulerAngles = new Vector3(4f, 5f, 6f);
            var writer = new FakeWriter();
            var controller = CreateController(
                config,
                new FakeRecipeSampler(recipe),
                new FakeSceneBuilder(generatedObjects),
                new FakeProjector(true),
                writer);

            yield return controller.GenerateRun("mapping-run", 1);

            var artifact = writer.Artifacts[0];
            Assert.That(artifact.FrameId, Is.EqualTo(1));
            Assert.That(artifact.Manifest.frame_id, Is.EqualTo(1));
            Assert.That(artifact.Manifest.background_id, Is.EqualTo("forest"));
            Assert.That(artifact.Manifest.objects, Has.Length.EqualTo(1));
            Assert.That(artifact.Manifest.objects[0].class_id, Is.EqualTo(2));
            Assert.That(artifact.Manifest.objects[0].class_name, Is.EqualTo("squirtle"));
            Assert.That(artifact.Manifest.objects[0].center, Is.EqualTo(recipe.Objects[0].Center));
            Assert.That(artifact.Manifest.objects[0].distance, Is.EqualTo(3.5f));
            Assert.That(artifact.Manifest.objects[0].scale, Is.EqualTo(1.1f));
            Assert.That(artifact.Manifest.objects[0].euler_angles, Is.EqualTo(new Vector3(4f, 5f, 6f)));
        }

        [UnityTest]
        public IEnumerator RealCaptureProducesReadablePngWithMatchingAnnotation()
        {
            var config = CreateConfig(1);
            var runDirectory = Path.Combine(
                Application.temporaryCachePath,
                "SyntheticDataTests",
                Guid.NewGuid().ToString("N"));
            temporaryDirectories.Add(runDirectory);
            var writer = new AtomicWriterAdapter(new AtomicCaptureWriter(runDirectory));
            var builder = new RenderableFrameBuilder(generatedObjects, materials);
            var camera = CreateCamera();
            var backgroundTexture = CreateTexture("forest");
            textures.Add(backgroundTexture);
            var captureService = new CameraCaptureService();
            var controller = new SyntheticCaptureController(
                camera,
                config,
                new FakeRecipeSampler(CreateRecipe(0, 74)),
                builder,
                new MeshBoundsProjector(4f, 0.25f),
                captureService,
                writer,
                new BackgroundCatalog(backgroundTexture));

            Assert.That(captureService.CapturePng(camera, config.Width, config.Height), Is.Not.Empty);
            yield return null;
            var renderTextureCountBefore = Resources.FindObjectsOfTypeAll<RenderTexture>().Length;

            yield return controller.GenerateRun("integration-run", 1);
            var generatedRoot = builder.LastBuilt;
            yield return null;

            var paths = new CapturePaths(runDirectory, 1);
            Assert.That(paths.ImagePath, Does.EndWith("frame_000001.png"));
            Assert.That(paths.LabelPath, Does.EndWith("frame_000001.txt"));
            Assert.That(File.Exists(paths.ImagePath), Is.True);
            Assert.That(File.Exists(paths.LabelPath), Is.True);
            Assert.That(File.Exists(paths.ManifestPath), Is.True);
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            textures.Add(decoded);
            Assert.That(decoded.LoadImage(File.ReadAllBytes(paths.ImagePath)), Is.True);
            Assert.That(decoded.width, Is.EqualTo(config.Width));
            Assert.That(decoded.height, Is.EqualTo(config.Height));
            Assert.That(decoded.GetPixel(config.Width / 2, config.Height / 2).r, Is.GreaterThan(0.9f));
            var labelParts = File.ReadAllText(paths.LabelPath)
                .Trim()
                .Split(' ');
            Assert.That(labelParts, Has.Length.EqualTo(5));
            Assert.That(labelParts[0], Is.EqualTo("0"));
            var labelBox = new float[4];
            for (var index = 0; index < labelBox.Length; index++)
            {
                labelBox[index] = float.Parse(labelParts[index + 1], CultureInfo.InvariantCulture);
            }

            Assert.That(labelBox[0], Is.EqualTo(0.5f).Within(0.02f));
            Assert.That(labelBox[1], Is.EqualTo(0.5f).Within(0.02f));
            Assert.That(labelBox[2], Is.GreaterThan(0f));
            Assert.That(labelBox[3], Is.GreaterThan(0f));
            AssertRenderedPixelsFitInsideBox(decoded, labelBox, 2);
            var manifestLines = File.ReadAllLines(paths.ManifestPath);
            Assert.That(manifestLines, Has.Length.EqualTo(1));
            var manifest = JsonUtility.FromJson<ManifestRecord>(manifestLines[0]);
            Assert.That(manifest.frame_id, Is.EqualTo(1));
            Assert.That(manifest.objects, Has.Length.EqualTo(1));
            Assert.That(manifest.objects[0].bbox, Is.EqualTo(labelBox).Within(0.000001f));
            Assert.That(builder.ClearCount, Is.EqualTo(1));
            Assert.That(generatedRoot == null, Is.True);
            Assert.That(
                Resources.FindObjectsOfTypeAll<RenderTexture>().Length,
                Is.EqualTo(renderTextureCountBefore));
        }

        [UnityTest]
        public IEnumerator AcceptsZeroObjectFrame()
        {
            var config = CreateConfig(1);
            var recipe = CreateRecipe(0, 31);
            recipe.Objects.Clear();
            var writer = new FakeWriter();
            var controller = CreateController(
                config,
                new FakeRecipeSampler(recipe),
                new FakeSceneBuilder(generatedObjects),
                new FakeProjector(true),
                writer);

            yield return controller.GenerateRun("negative-run", 1);

            Assert.That(writer.Artifacts, Has.Count.EqualTo(1));
            Assert.That(writer.Artifacts[0].Boxes, Is.Empty);
            Assert.That(writer.Artifacts[0].Manifest.objects, Is.Empty);
        }

        [UnityTest]
        public IEnumerator PublishesAcceptedNormalizedBoxesForThePreviewOverlay()
        {
            IReadOnlyList<YoloBox> previewBoxes = null;
            var previewClearCount = 0;
            var background = CreateTexture("forest-preview");
            textures.Add(background);
            var builder = new FakeSceneBuilder(generatedObjects);
            var controller = new SyntheticCaptureController(
                CreateCamera(),
                CreateConfig(1),
                new FakeRecipeSampler(CreateRecipe(0, 34)),
                builder,
                new FakeProjector(true),
                new FakeCapture(),
                new FakeWriter(),
                new BackgroundCatalog(background),
                boxes => previewBoxes = boxes,
                () => previewClearCount++);

            var routine = controller.GenerateRun("preview-run", 1);
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(previewClearCount, Is.EqualTo(1));
            yield return routine.Current;

            Assert.That(routine.MoveNext(), Is.True, "Accepted previews must remain visible for a frame.");

            Assert.That(previewBoxes, Has.Count.EqualTo(1));
            Assert.That(previewBoxes[0].ClassId, Is.EqualTo(0));
            Assert.That(previewBoxes[0].CenterX, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(builder.ClearCount, Is.Zero);

            yield return routine.Current;
            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(builder.ClearCount, Is.EqualTo(1));
            Assert.That(previewClearCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator RejectsWholeFrameWhenAnyObjectProjectionFails()
        {
            var config = CreateConfig(1);
            var recipe = CreateRecipe(0, 41);
            recipe.Objects.Add(new ObjectRecipe { ClassId = 1 });
            var builder = new FakeSceneBuilder(generatedObjects);
            var writer = new FakeWriter();
            var controller = CreateController(
                config,
                new FakeRecipeSampler(recipe),
                builder,
                new FakeProjector(true, false),
                writer);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                RunToCompletion(controller.GenerateRun("whole-frame-run", 1)));

            Assert.That(exception.Message, Does.Contain("frame 1"));
            Assert.That(writer.Artifacts, Is.Empty);
            Assert.That(controller.Rejections, Has.Count.EqualTo(1));
            Assert.That(builder.ClearCount, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MixedDeviceCaptureRetriesAtProjectedBoxContainmentThreshold()
        {
            var config = CreateConfig(2);
            config.ApplyMixedDeviceSettings();
            config.Width = 640;
            config.Height = 640;
            var firstAttempt = CreateTwoObjectRecipe(0, 91);
            var retry = CreateTwoObjectRecipe(0, 92);
            var projector = new BoundsProjector(
                new PixelBounds(50f, 50f, 195f, 250f),
                new PixelBounds(100f, 100f, 200f, 200f),
                new PixelBounds(50f, 50f, 250f, 250f),
                new PixelBounds(300f, 300f, 400f, 400f));
            var writer = new FakeWriter();
            var controller = CreateController(
                config,
                new FakeRecipeSampler(firstAttempt, retry),
                new FakeSceneBuilder(generatedObjects),
                projector,
                writer);

            yield return controller.GenerateRun("containment-run", 1);

            Assert.That(writer.Artifacts, Has.Count.EqualTo(1));
            Assert.That(controller.Rejections, Has.Count.EqualTo(1));
            Assert.That(controller.Rejections[0].Reason, Does.Contain("objects 0 and 1").IgnoreCase);
            Assert.That(controller.Rejections[0].Reason, Does.Contain("0.950"));
        }

        [UnityTest]
        public IEnumerator LegacyCaptureAllowsProjectedBoxContainment()
        {
            var config = CreateConfig(1);
            config.Width = 640;
            config.Height = 640;
            var writer = new FakeWriter();
            var controller = CreateController(
                config,
                new FakeRecipeSampler(CreateTwoObjectRecipe(0, 93)),
                new FakeSceneBuilder(generatedObjects),
                new BoundsProjector(
                    new PixelBounds(50f, 50f, 250f, 250f),
                    new PixelBounds(100f, 100f, 200f, 200f)),
                writer);

            yield return controller.GenerateRun("legacy-containment-run", 1);

            Assert.That(writer.Artifacts, Has.Count.EqualTo(1));
            Assert.That(controller.Rejections, Is.Empty);
        }

        [UnityTest]
        public IEnumerator ExhaustionIncludesActionableLastRejectionAndCleansEveryAttempt()
        {
            var config = CreateConfig(2);
            var sampler = new FakeRecipeSampler(CreateRecipe(0, 51), CreateRecipe(0, 52));
            var builder = new FakeSceneBuilder(generatedObjects);
            var controller = CreateController(
                config,
                sampler,
                builder,
                new FakeProjector(false, false),
                new FakeWriter());

            var exception = Assert.Throws<InvalidOperationException>(() =>
                RunToCompletion(controller.GenerateRun("exhausted-run", 1)));

            Assert.That(exception.Message, Does.Contain("MaximumFrameRetries"));
            Assert.That(exception.Message, Does.Contain("frame 1"));
            Assert.That(exception.Message, Does.Contain("seed 52"));
            Assert.That(builder.ClearCount, Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CapturesOnlyAfterAnEndOfFrameBoundary()
        {
            var config = CreateConfig(1);
            var capture = new FrameAwareCapture();
            var controller = CreateController(
                config,
                new FakeRecipeSampler(CreateRecipe(0, 61)),
                new FakeSceneBuilder(generatedObjects),
                new FakeProjector(true),
                new FakeWriter(),
                capture);
            var routine = controller.GenerateRun("timing-run", 1);

            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(routine.Current, Is.TypeOf<WaitForEndOfFrame>());
            Assert.That(capture.CaptureFrame, Is.EqualTo(-1));

            yield return routine.Current;

            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(capture.CaptureFrame, Is.GreaterThanOrEqualTo(0));
        }

        [UnityTest]
        public IEnumerator ClearsGeneratedSceneWhenWriterThrows()
        {
            var config = CreateConfig(1);
            var builder = new FakeSceneBuilder(generatedObjects);
            var controller = CreateController(
                config,
                new FakeRecipeSampler(CreateRecipe(0, 81)),
                builder,
                new FakeProjector(true),
                new ThrowingWriter());

            Assert.Throws<InvalidOperationException>(() =>
                RunToCompletion(controller.GenerateRun("exception-run", 1)));

            Assert.That(builder.ClearCount, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClearsGeneratedSceneWhenCaptureThrows()
        {
            var config = CreateConfig(1);
            var builder = new FakeSceneBuilder(generatedObjects);
            var controller = CreateController(
                config,
                new FakeRecipeSampler(CreateRecipe(0, 82)),
                builder,
                new FakeProjector(true),
                new FakeWriter(),
                new ThrowingCapture());

            Assert.Throws<InvalidOperationException>(() =>
                RunToCompletion(controller.GenerateRun("capture-exception-run", 1)));

            Assert.That(builder.ClearCount, Is.EqualTo(1));
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var generatedObject in generatedObjects)
            {
                if (generatedObject != null)
                {
                    Object.DestroyImmediate(generatedObject);
                }
            }

            generatedObjects.Clear();
            foreach (var generationConfig in generationConfigs)
            {
                Object.DestroyImmediate(generationConfig);
            }

            generationConfigs.Clear();
            foreach (var testMaterial in materials)
            {
                Object.DestroyImmediate(testMaterial);
            }

            materials.Clear();
            foreach (var texture in textures)
            {
                Object.DestroyImmediate(texture);
            }

            textures.Clear();
            foreach (var temporaryDirectory in temporaryDirectories)
            {
                if (Directory.Exists(temporaryDirectory))
                {
                    Directory.Delete(temporaryDirectory, true);
                }
            }

            temporaryDirectories.Clear();
        }

        private SyntheticCaptureController CreateController(
            GenerationConfig config,
            IFrameRecipeSampler sampler,
            IFrameSceneBuilder builder,
            IBoundsProjector projector,
            IFrameArtifactWriter writer,
            ICameraCaptureService capture = null)
        {
            var camera = CreateCamera();
            var backgroundTexture = CreateTexture("forest");
            textures.Add(backgroundTexture);
            return new SyntheticCaptureController(
                camera,
                config,
                sampler,
                builder,
                projector,
                capture ?? new FakeCapture(),
                writer,
                new BackgroundCatalog(backgroundTexture));
        }

        private Camera CreateCamera()
        {
            var cameraObject = new GameObject("Controller Camera");
            generatedObjects.Add(cameraObject);
            cameraObject.transform.position = new Vector3(0f, 0f, -5f);
            cameraObject.transform.rotation = Quaternion.identity;
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = 60f;
            camera.aspect = 1f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            return camera;
        }

        private GenerationConfig CreateConfig(int retries)
        {
            var config = GenerationConfig.CreateTestDefault(99);
            generationConfigs.Add(config);
            config.MaximumFrameRetries = retries;
            config.BackgroundCount = 1;
            config.Width = 64;
            config.Height = 64;
            return config;
        }

        private static void AssertRenderedPixelsFitInsideBox(Texture2D image, float[] yoloBox, int tolerance)
        {
            var minimumX = image.width;
            var minimumY = image.height;
            var maximumX = -1;
            var maximumY = -1;
            for (var y = 0; y < image.height; y++)
            {
                for (var x = 0; x < image.width; x++)
                {
                    var pixel = image.GetPixel(x, y);
                    if (pixel.r <= 0.5f || pixel.g >= 0.2f || pixel.b >= 0.2f)
                    {
                        continue;
                    }

                    minimumX = Mathf.Min(minimumX, x);
                    minimumY = Mathf.Min(minimumY, y);
                    maximumX = Mathf.Max(maximumX, x);
                    maximumY = Mathf.Max(maximumY, y);
                }
            }

            Assert.That(maximumX, Is.GreaterThanOrEqualTo(0), "Expected rendered red pixels.");
            var boxMinimumX = (yoloBox[0] - yoloBox[2] * 0.5f) * image.width;
            var boxMaximumX = (yoloBox[0] + yoloBox[2] * 0.5f) * image.width;
            var boxMinimumY = (yoloBox[1] - yoloBox[3] * 0.5f) * image.height;
            var boxMaximumY = (yoloBox[1] + yoloBox[3] * 0.5f) * image.height;
            Assert.That(minimumX, Is.GreaterThanOrEqualTo(boxMinimumX - tolerance));
            Assert.That(maximumX, Is.LessThanOrEqualTo(boxMaximumX + tolerance));
            Assert.That(minimumY, Is.GreaterThanOrEqualTo(boxMinimumY - tolerance));
            Assert.That(maximumY, Is.LessThanOrEqualTo(boxMaximumY + tolerance));
        }

        private static FrameRecipe CreateRecipe(int frameIndex, int seed)
        {
            return new FrameRecipe
            {
                FrameIndex = frameIndex,
                AttemptIndex = 0,
                Seed = seed,
                Background = new BackgroundRecipe { Index = 0 },
                Objects = new List<ObjectRecipe> { new ObjectRecipe { ClassId = 0 } }
            };
        }

        private static FrameRecipe CreateTwoObjectRecipe(int frameIndex, int seed)
        {
            var recipe = CreateRecipe(frameIndex, seed);
            recipe.Objects[0].SizeBand = ObjectSizeBand.Large;
            recipe.Objects.Add(new ObjectRecipe
            {
                ClassId = 1,
                SizeBand = ObjectSizeBand.Medium
            });
            return recipe;
        }

        private static Texture2D CreateTexture(string name)
        {
            var texture = new Texture2D(2, 2) { name = name };
            return texture;
        }

        private static void RunToCompletion(IEnumerator routine)
        {
            while (routine.MoveNext())
            {
            }
        }

        private sealed class FakeRecipeSampler : IFrameRecipeSampler
        {
            private readonly FrameRecipe[] recipes;
            public int Attempts { get; private set; }

            public FakeRecipeSampler(params FrameRecipe[] recipes)
            {
                this.recipes = recipes;
            }

            public FrameRecipe Sample(int frameIndex, int attemptIndex)
            {
                Attempts++;
                var source = recipes[Mathf.Min(attemptIndex, recipes.Length - 1)];
                return new FrameRecipe
                {
                    FrameIndex = frameIndex,
                    AttemptIndex = attemptIndex,
                    Seed = source.Seed,
                    Background = source.Background,
                    Objects = source.Objects,
                    CameraFov = source.CameraFov,
                    Light = source.Light
                };
            }
        }

        private sealed class FakeSceneBuilder : IFrameSceneBuilder
        {
            private readonly List<GameObject> owner;
            public int ClearCount { get; private set; }

            public FakeSceneBuilder(List<GameObject> owner)
            {
                this.owner = owner;
            }

            public IGeneratedFrame Build(FrameRecipe recipe)
            {
                var objects = new List<GameObject>();
                foreach (var unused in recipe.Objects)
                {
                    var generated = new GameObject("Generated object");
                    owner.Add(generated);
                    objects.Add(generated);
                }

                return new FakeGeneratedFrame(objects);
            }

            public void Clear(IGeneratedFrame frame)
            {
                ClearCount++;
            }
        }

        private sealed class FakeGeneratedFrame : IGeneratedFrame
        {
            public FakeGeneratedFrame(IReadOnlyList<GameObject> objects)
            {
                Objects = objects;
            }

            public IReadOnlyList<GameObject> Objects { get; }
        }

        private sealed class RenderableFrameBuilder : IFrameSceneBuilder
        {
            private readonly List<GameObject> owners;
            private readonly List<Material> materials;

            public RenderableFrameBuilder(List<GameObject> owners, List<Material> materials)
            {
                this.owners = owners;
                this.materials = materials;
            }

            public int ClearCount { get; private set; }
            public GameObject LastBuilt { get; private set; }

            public IGeneratedFrame Build(FrameRecipe recipe)
            {
                LastBuilt = GameObject.CreatePrimitive(PrimitiveType.Cube);
                LastBuilt.name = "Rendered Pokemon Test Object";
                LastBuilt.transform.position = Vector3.zero;
                var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetColor("_BaseColor", Color.red);
                LastBuilt.GetComponent<Renderer>().sharedMaterial = material;
                owners.Add(LastBuilt);
                materials.Add(material);
                return new FakeGeneratedFrame(new[] { LastBuilt });
            }

            public void Clear(IGeneratedFrame frame)
            {
                ClearCount++;
                foreach (var generatedObject in frame.Objects)
                {
                    Object.Destroy(generatedObject);
                }
            }
        }

        private sealed class FakeProjector : IBoundsProjector
        {
            private readonly Queue<bool> outcomes;

            public FakeProjector(params bool[] outcomes)
            {
                this.outcomes = new Queue<bool>(outcomes);
            }

            public bool TryProject(
                Camera camera,
                GameObject target,
                int width,
                int height,
                out PixelBounds bounds)
            {
                var valid = outcomes.Count == 0 || outcomes.Dequeue();
                bounds = new PixelBounds(8f, 8f, 56f, 56f);
                return valid;
            }
        }

        private sealed class BoundsProjector : IBoundsProjector
        {
            private readonly Queue<PixelBounds> bounds;

            public BoundsProjector(params PixelBounds[] bounds)
            {
                this.bounds = new Queue<PixelBounds>(bounds);
            }

            public bool TryProject(
                Camera camera,
                GameObject target,
                int width,
                int height,
                out PixelBounds projectedBounds)
            {
                projectedBounds = bounds.Dequeue();
                return true;
            }
        }

        private class FakeCapture : ICameraCaptureService
        {
            public virtual byte[] CapturePng(Camera camera, int width, int height)
            {
                return new byte[] { 1, 2, 3 };
            }
        }

        private sealed class FrameAwareCapture : FakeCapture
        {
            public int CaptureFrame { get; private set; } = -1;

            public override byte[] CapturePng(Camera camera, int width, int height)
            {
                CaptureFrame = Time.frameCount;
                return base.CapturePng(camera, width, height);
            }
        }

        private sealed class ThrowingCapture : FakeCapture
        {
            public override byte[] CapturePng(Camera camera, int width, int height)
            {
                throw new InvalidOperationException("capture failed");
            }
        }

        private sealed class FakeWriter : IFrameArtifactWriter
        {
            public List<FrameArtifact> Artifacts { get; } = new List<FrameArtifact>();

            public void Write(FrameArtifact artifact)
            {
                Artifacts.Add(artifact);
            }
        }

        private sealed class AtomicWriterAdapter : IFrameArtifactWriter
        {
            private readonly AtomicCaptureWriter writer;

            public AtomicWriterAdapter(AtomicCaptureWriter writer)
            {
                this.writer = writer;
            }

            public void Write(FrameArtifact artifact)
            {
                writer.Write(artifact);
            }
        }

        private sealed class ThrowingWriter : IFrameArtifactWriter
        {
            public void Write(FrameArtifact artifact)
            {
                throw new InvalidOperationException("writer failed");
            }
        }
    }
}
