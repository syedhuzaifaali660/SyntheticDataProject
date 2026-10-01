using System;
using NUnit.Framework;
using SyntheticData.Domain;
using SyntheticData.Generation;
using SyntheticData.Scene;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SyntheticData.Tests
{
    public sealed class FrameSceneBuilderTests
    {
        private readonly GameObject[] objectsToDestroy = new GameObject[32];
        private int objectCount;
        private readonly Texture2D[] texturesToDestroy = new Texture2D[8];
        private int textureCount;
        private readonly Material[] materialsToDestroy = new Material[8];
        private int materialCount;
        private float originalAmbientIntensity;
        private AmbientMode originalAmbientMode;
        private Color originalAmbientLight;

        [SetUp]
        public void SetUp()
        {
            originalAmbientIntensity = RenderSettings.ambientIntensity;
            originalAmbientMode = RenderSettings.ambientMode;
            originalAmbientLight = RenderSettings.ambientLight;
        }

        [TearDown]
        public void TearDown()
        {
            RenderSettings.ambientIntensity = originalAmbientIntensity;
            RenderSettings.ambientMode = originalAmbientMode;
            RenderSettings.ambientLight = originalAmbientLight;
            for (var index = objectCount - 1; index >= 0; index--)
            {
                if (objectsToDestroy[index] != null)
                {
                    Object.DestroyImmediate(objectsToDestroy[index]);
                }
            }

            for (var index = textureCount - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(texturesToDestroy[index]);
            }

            for (var index = materialCount - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(materialsToDestroy[index]);
            }

            objectCount = 0;
            textureCount = 0;
            materialCount = 0;
        }

        [Test]
        public void MixedDeviceRealPrefabsFitRequestedBandsAndStayInsideEveryProfile()
        {
            var camera = CreateCamera(new Vector3(0, 0, -5));
            var background = CreateBackgroundRenderer();
            var originalPosition = background.transform.position;
            var originalScale = background.transform.localScale;
            var builder = CreateBuilder(camera, background, CreatePointLight(),
                new PokemonPrefabCatalog.Entry(PokemonClass.Pikachu, AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SyntheticData/Prefabs/Pikachu.prefab")),
                new PokemonPrefabCatalog.Entry(PokemonClass.Charmander, AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SyntheticData/Prefabs/Charmander.prefab")),
                new PokemonPrefabCatalog.Entry(PokemonClass.Squirtle, AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SyntheticData/Prefabs/Squirtle.prefab")),
                CreateTexture("one.png"), CreateTexture("two.png"));
            var config = GenerationConfig.CreateMixedDeviceDefault(42);
            config.BackgroundCount = 2;
            var sampler = new FrameRecipeSampler(config);
            try
            {
                // Includes the exact previously failing frame indices 42 and 174.
                for (var frameIndex = 0; frameIndex < 6000; frameIndex++)
                {
                    var recipe = sampler.Sample(frameIndex);
                    var frame = builder.Build(recipe);
                    try
                    {
                        var lower = camera.WorldToViewportPoint(background.transform.TransformPoint(new Vector3(-.5f, -.5f, 0)));
                        var upper = camera.WorldToViewportPoint(background.transform.TransformPoint(new Vector3(.5f, .5f, 0)));
                        Assert.That(lower.x, Is.LessThanOrEqualTo(0));
                        Assert.That(lower.y, Is.LessThanOrEqualTo(0));
                        Assert.That(upper.x, Is.GreaterThanOrEqualTo(1));
                        Assert.That(upper.y, Is.GreaterThanOrEqualTo(1));
                        for (var i = 0; i < frame.Roots.Count; i++)
                        {
                            var context = $"frame {frameIndex + 1}, object {i}, {recipe.CaptureProfile.Name}, {recipe.Objects[i].SizeBand}";
                            var projector = new SyntheticData.Labels.MeshBoundsProjector(8f, 0f);
                            Assert.That(projector.TryProject(camera, frame.Roots[i], recipe.OutputWidth, recipe.OutputHeight, out var box), Is.True, context);
                            var side = Mathf.Max(box.Width, box.Height) * 640f / Mathf.Max(recipe.OutputWidth, recipe.OutputHeight);
                            var band = recipe.Objects[i].SizeBand;
                            var minimum = band == ObjectSizeBand.Small ? 8f : band == ObjectSizeBand.Medium ? 72f : 192f;
                            var maximum = band == ObjectSizeBand.Small ? 72f : band == ObjectSizeBand.Medium ? 192f : 448f;
                            Assert.That(side, Is.InRange(minimum, maximum), context);
                        }
                    }
                    finally { builder.Clear(frame); }
                    AssertVector(background.transform.position, originalPosition);
                    AssertVector(background.transform.localScale, originalScale);
                }
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void BuildAppliesLiteralRecipeTransformsLabelsCachesAndSceneState()
        {
            const float outputAspect = 2f;
            var camera = CreateCamera(new Vector3(2f, 3f, -8f));
            camera.fieldOfView = 61f;
            camera.aspect = 1f;
            var backgroundRenderer = CreateBackgroundRenderer();
            var pointLight = CreatePointLight();
            pointLight.transform.localPosition = new Vector3(9f, 8f, 7f);
            pointLight.intensity = 0.25f;
            pointLight.colorTemperature = 3000f;
            RenderSettings.ambientIntensity = 0.42f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientLight = new Color(0.12f, 0.23f, 0.34f);

            var pikachu = CreateLabeledCube("Pikachu Source", PokemonClass.Pikachu);
            var charmander = CreateLabeledCube("Charmander Source", PokemonClass.Charmander);
            var squirtle = CreateLabeledCube("Squirtle Source", PokemonClass.Squirtle);
            var alpha = CreateTexture("alpha.jpg");
            var zebra = CreateTexture("zebra.png");
            var builder = CreateBuilder(
                camera,
                backgroundRenderer,
                pointLight,
                new PokemonPrefabCatalog.Entry(PokemonClass.Pikachu, pikachu),
                new PokemonPrefabCatalog.Entry(PokemonClass.Charmander, charmander),
                new PokemonPrefabCatalog.Entry(PokemonClass.Squirtle, squirtle),
                alpha,
                zebra);
            var recipe = CreateLiteralRecipe();

            var frame = builder.Build(recipe);

            Assert.That(frame.GeneratedObjectsRoot.name, Is.EqualTo("GeneratedObjects"));
            Assert.That(frame.Roots.Count, Is.EqualTo(3));
            Assert.That(frame.SpawnedObjects.Count, Is.EqualTo(3));
            for (var index = 0; index < recipe.Objects.Count; index++)
            {
                var expected = recipe.Objects[index];
                var root = frame.Roots[index];
                var expectedPosition = GetExpectedWorldPosition(
                    camera,
                    expected.Center,
                    expected.Distance,
                    outputAspect);

                Assert.That(root.transform.parent, Is.EqualTo(frame.GeneratedObjectsRoot));
                Assert.That(root.GetComponent<PokemonLabel>().ClassId, Is.EqualTo(expected.ClassId));
                AssertVector(root.transform.position, expectedPosition);
                Assert.That(
                    Vector3.Distance(camera.transform.position, root.transform.position),
                    Is.EqualTo(expected.Distance).Within(0.0001f));
                AssertVector(root.transform.localScale, Vector3.one * expected.Scale);
                AssertQuaternion(root.transform.rotation, Quaternion.Euler(expected.EulerAngles));
                Assert.That(frame.SpawnedObjects[index].VertexCache.Entries, Is.Not.Empty);
                Assert.That(frame.GetCache(root), Is.SameAs(frame.SpawnedObjects[index].VertexCache));
            }

            Assert.That(camera.fieldOfView, Is.EqualTo(recipe.CameraFov));
            Assert.That(camera.aspect, Is.EqualTo(outputAspect));
            AssertVector(pointLight.transform.localPosition, recipe.Light.PointPosition);
            Assert.That(pointLight.intensity, Is.EqualTo(recipe.Light.PointIntensity));
            Assert.That(pointLight.colorTemperature, Is.EqualTo(recipe.Light.Temperature));
            Assert.That(RenderSettings.ambientIntensity, Is.EqualTo(recipe.Light.AmbientIntensity));
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Flat));
            Assert.That(RenderSettings.ambientLight, Is.EqualTo(Color.white));
            var properties = new MaterialPropertyBlock();
            backgroundRenderer.GetPropertyBlock(properties);
            Assert.That(properties.GetTexture("_BaseMap"), Is.EqualTo(zebra));
            var backgroundTransform = properties.GetVector("_BaseMap_ST");
            AssertVector(backgroundTransform, new Vector4(0.8f, 0.8f, 0.2f, 0.05f));
            Assert.That(backgroundTransform.z, Is.GreaterThanOrEqualTo(0f));
            Assert.That(backgroundTransform.w, Is.GreaterThanOrEqualTo(0f));
            Assert.That(backgroundTransform.x + backgroundTransform.z, Is.LessThanOrEqualTo(1f));
            Assert.That(backgroundTransform.y + backgroundTransform.w, Is.LessThanOrEqualTo(1f));
            Assert.That(properties.GetFloat("_Brightness"), Is.EqualTo(1.1f));
            Assert.That(properties.GetFloat("_Contrast"), Is.EqualTo(0.9f));
            Assert.That(properties.GetFloat("_Blur"), Is.EqualTo(0.25f));
            Assert.That(properties.GetFloat("_Temperature"), Is.EqualTo(6200f));
            builder.Clear(frame);
            Assert.That(camera.aspect, Is.EqualTo(1f));
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Trilight));
            Assert.That(RenderSettings.ambientLight, Is.EqualTo(new Color(0.12f, 0.23f, 0.34f)));
        }

        [Test]
        public void CatalogsRejectMissingDuplicateAndLabelMismatchedPokemonBeforeBuild()
        {
            var pikachu = CreateLabeledCube("Pikachu Source", PokemonClass.Pikachu);
            var charmander = CreateLabeledCube("Charmander Source", PokemonClass.Charmander);
            var squirtle = CreateLabeledCube("Squirtle Source", PokemonClass.Squirtle);

            Assert.That(
                () => new PokemonPrefabCatalog(
                    new PokemonPrefabCatalog.Entry(PokemonClass.Pikachu, pikachu),
                    new PokemonPrefabCatalog.Entry(PokemonClass.Charmander, charmander)),
                Throws.InvalidOperationException.With.Message.Contains("missing"));
            Assert.That(
                () => new PokemonPrefabCatalog(
                    new PokemonPrefabCatalog.Entry(PokemonClass.Pikachu, pikachu),
                    new PokemonPrefabCatalog.Entry(PokemonClass.Pikachu, pikachu),
                    new PokemonPrefabCatalog.Entry(PokemonClass.Squirtle, squirtle)),
                Throws.InvalidOperationException.With.Message.Contains("duplicate"));
            Assert.That(
                () => new PokemonPrefabCatalog(
                    new PokemonPrefabCatalog.Entry(PokemonClass.Pikachu, charmander),
                    new PokemonPrefabCatalog.Entry(PokemonClass.Charmander, pikachu),
                    new PokemonPrefabCatalog.Entry(PokemonClass.Squirtle, squirtle)),
                Throws.InvalidOperationException.With.Message.Contains("label"));
        }

        [Test]
        public void BackgroundCatalogSortsExtensionFreeIdsAndRejectsDuplicateOrInvalidIds()
        {
            var zebra = CreateTexture("zebra.png");
            var alpha = CreateTexture("alpha.jpg");
            var catalog = new BackgroundCatalog(zebra, alpha);

            Assert.That(catalog.BackgroundIds, Is.EqualTo(new[] { "alpha", "zebra" }));
            Assert.That(catalog.Get(0), Is.EqualTo(alpha));
            Assert.That(catalog.GetId(1), Is.EqualTo("zebra"));
            Assert.That(
                () => new BackgroundCatalog(CreateTexture("same.png"), CreateTexture("same.jpg")),
                Throws.InvalidOperationException.With.Message.Contains("duplicate"));
            Assert.That(
                () => new BackgroundCatalog(CreateTexture(string.Empty)),
                Throws.InvalidOperationException.With.Message.Contains("invalid"));
            Assert.That(
                () => new BackgroundCatalog().Validate(),
                Throws.InvalidOperationException.With.Message.Contains("at least one"));
        }

        [Test]
        public void ClearDestroysOnlyFrameRootsAndRestoresUserAuthoredSceneState()
        {
            var camera = CreateCamera(new Vector3(0f, 1f, -10f));
            camera.fieldOfView = 63f;
            var backgroundRenderer = CreateBackgroundRenderer();
            var originalProperties = new MaterialPropertyBlock();
            originalProperties.SetFloat("_Brightness", 0.37f);
            backgroundRenderer.SetPropertyBlock(originalProperties);
            var pointLight = CreatePointLight();
            pointLight.transform.localPosition = new Vector3(6f, 5f, 4f);
            pointLight.transform.localRotation = Quaternion.Euler(12f, 13f, 14f);
            pointLight.transform.localScale = new Vector3(2f, 3f, 4f);
            pointLight.intensity = 0.27f;
            pointLight.colorTemperature = 3700f;
            RenderSettings.ambientIntensity = 0.33f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientLight = new Color(0.12f, 0.23f, 0.34f);
            var userObject = Track(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            userObject.name = "User Authored Sphere";
            var builder = CreateDefaultBuilder(camera, backgroundRenderer, pointLight);

            var frame = builder.Build(CreateLiteralRecipe());
            builder.Clear(frame);

            Assert.That(frame.GeneratedObjectsRoot == null, Is.True);
            foreach (var root in frame.Roots)
            {
                Assert.That(root == null, Is.True);
            }
            Assert.That(userObject, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            Assert.That(backgroundRenderer, Is.Not.Null);
            Assert.That(pointLight, Is.Not.Null);
            Assert.That(camera.fieldOfView, Is.EqualTo(63f));
            AssertVector(pointLight.transform.localPosition, new Vector3(6f, 5f, 4f));
            AssertQuaternion(pointLight.transform.localRotation, Quaternion.Euler(12f, 13f, 14f));
            AssertVector(pointLight.transform.localScale, new Vector3(2f, 3f, 4f));
            Assert.That(pointLight.intensity, Is.EqualTo(0.27f));
            Assert.That(pointLight.colorTemperature, Is.EqualTo(3700f));
            Assert.That(RenderSettings.ambientIntensity, Is.EqualTo(0.33f));
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Trilight));
            Assert.That(RenderSettings.ambientLight, Is.EqualTo(new Color(0.12f, 0.23f, 0.34f)));
            var restored = new MaterialPropertyBlock();
            backgroundRenderer.GetPropertyBlock(restored);
            Assert.That(restored.GetFloat("_Brightness"), Is.EqualTo(0.37f));
        }

        [Test]
        public void BuildUsesAndPreservesTheAuthoredGeneratedObjectsContainer()
        {
            var camera = CreateCamera(new Vector3(0f, 0f, -5f));
            var backgroundRenderer = CreateBackgroundRenderer();
            var pointLight = CreatePointLight();
            var container = Track(new GameObject("GeneratedObjects"));
            var builder = new FrameSceneBuilder(
                camera,
                backgroundRenderer,
                pointLight,
                new PokemonPrefabCatalog(
                    new PokemonPrefabCatalog.Entry(PokemonClass.Pikachu, CreateLabeledCube("Pikachu Source", PokemonClass.Pikachu)),
                    new PokemonPrefabCatalog.Entry(PokemonClass.Charmander, CreateLabeledCube("Charmander Source", PokemonClass.Charmander)),
                    new PokemonPrefabCatalog.Entry(PokemonClass.Squirtle, CreateLabeledCube("Squirtle Source", PokemonClass.Squirtle))),
                new BackgroundCatalog(
                    CreateTexture("background.png"),
                    CreateTexture("background-two.png")),
                1f,
                container.transform);

            var frame = builder.Build(CreateLiteralRecipe());

            Assert.That(frame.GeneratedObjectsRoot.name, Is.EqualTo("GeneratedFrame"));
            Assert.That(frame.GeneratedObjectsRoot.parent, Is.EqualTo(container.transform));
            builder.Clear(frame);
            Assert.That(container, Is.Not.Null);
            Assert.That(container.transform.childCount, Is.Zero);
        }

        [Test]
        public void ClearRestoresAutomaticCameraAspectBehavior()
        {
            var camera = CreateCamera(new Vector3(0f, 0f, -5f));
            var wideTarget = new RenderTexture(320, 160, 0);
            var tallTarget = new RenderTexture(160, 320, 0);
            try
            {
                camera.targetTexture = wideTarget;
                camera.ResetAspect();
                Assert.That(camera.aspect, Is.EqualTo(2f).Within(0.001f));
                var builder = new FrameSceneBuilder(
                    camera,
                    CreateBackgroundRenderer(),
                    CreatePointLight(),
                    new PokemonPrefabCatalog(
                        new PokemonPrefabCatalog.Entry(PokemonClass.Pikachu, CreateLabeledCube("Pikachu Source", PokemonClass.Pikachu)),
                        new PokemonPrefabCatalog.Entry(PokemonClass.Charmander, CreateLabeledCube("Charmander Source", PokemonClass.Charmander)),
                        new PokemonPrefabCatalog.Entry(PokemonClass.Squirtle, CreateLabeledCube("Squirtle Source", PokemonClass.Squirtle))),
                    new BackgroundCatalog(
                        CreateTexture("background.png"),
                        CreateTexture("background-two.png")),
                    1f);

                var frame = builder.Build(CreateLiteralRecipe());
                Assert.That(camera.aspect, Is.EqualTo(1f).Within(0.001f));
                builder.Clear(frame);
                camera.targetTexture = tallTarget;

                Assert.That(camera.aspect, Is.EqualTo(0.5f).Within(0.001f));
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(wideTarget);
                Object.DestroyImmediate(tallTarget);
            }
        }

        [Test]
        public void FailedBuildCleansCreatedRootsAndRestoresSceneState()
        {
            var camera = CreateCamera(new Vector3(0f, 0f, -10f));
            camera.fieldOfView = 64f;
            var backgroundRenderer = CreateBackgroundRenderer();
            var pointLight = CreatePointLight();
            pointLight.intensity = 0.36f;
            RenderSettings.ambientIntensity = 0.31f;
            var readable = CreateLabeledCube("Pikachu Source", PokemonClass.Pikachu);
            var unreadable = CreateUnreadableLabeledPrefab("Charmander Source", PokemonClass.Charmander);
            var squirtle = CreateLabeledCube("Squirtle Source", PokemonClass.Squirtle);
            var builder = CreateBuilder(
                camera,
                backgroundRenderer,
                pointLight,
                new PokemonPrefabCatalog.Entry(PokemonClass.Pikachu, readable),
                new PokemonPrefabCatalog.Entry(PokemonClass.Charmander, unreadable),
                new PokemonPrefabCatalog.Entry(PokemonClass.Squirtle, squirtle),
                CreateTexture("background.png"),
                CreateTexture("background-two.png"));
            var recipe = CreateLiteralRecipe();
            recipe.Objects.RemoveAt(2);

            Assert.That(() => builder.Build(recipe), Throws.InvalidOperationException.With.Message.Contains("unreadable"));
            Assert.That(GameObject.Find("GeneratedObjects"), Is.Null);
            Assert.That(camera.fieldOfView, Is.EqualTo(64f));
            Assert.That(pointLight.intensity, Is.EqualTo(0.36f));
            Assert.That(RenderSettings.ambientIntensity, Is.EqualTo(0.31f));
        }

        private FrameSceneBuilder CreateDefaultBuilder(Camera camera, Renderer backgroundRenderer, Light pointLight)
        {
            return CreateBuilder(
                camera,
                backgroundRenderer,
                pointLight,
                new PokemonPrefabCatalog.Entry(PokemonClass.Pikachu, CreateLabeledCube("Pikachu Source", PokemonClass.Pikachu)),
                new PokemonPrefabCatalog.Entry(PokemonClass.Charmander, CreateLabeledCube("Charmander Source", PokemonClass.Charmander)),
                new PokemonPrefabCatalog.Entry(PokemonClass.Squirtle, CreateLabeledCube("Squirtle Source", PokemonClass.Squirtle)),
                CreateTexture("background.png"),
                CreateTexture("background-two.png"));
        }

        private static FrameRecipe CreateLiteralRecipe()
        {
            return new FrameRecipe
            {
                CameraFov = 47f,
                Background = new BackgroundRecipe
                {
                    Index = 1,
                    CropOffset = new Vector2(0.1f, -0.05f),
                    Scale = 1.25f,
                    Brightness = 1.1f,
                    Contrast = 0.9f,
                    Blur = 0.25f,
                    Temperature = 6200f
                },
                Light = new LightRecipe
                {
                    AmbientIntensity = 0.82f,
                    PointIntensity = 1.75f,
                    PointPosition = new Vector3(2f, 4f, -1f),
                    Temperature = 6100f
                },
                Objects =
                {
                    new ObjectRecipe
                    {
                        ClassId = 0,
                        Center = new Vector2(0.2f, 0.3f),
                        Distance = 3f,
                        Scale = 1.1f,
                        EulerAngles = new Vector3(10f, 20f, 30f)
                    },
                    new ObjectRecipe
                    {
                        ClassId = 1,
                        Center = new Vector2(0.5f, 0.6f),
                        Distance = 4f,
                        Scale = 0.9f,
                        EulerAngles = new Vector3(-12f, 45f, 5f)
                    },
                    new ObjectRecipe
                    {
                        ClassId = 2,
                        Center = new Vector2(0.8f, 0.7f),
                        Distance = 2.5f,
                        Scale = 1.2f,
                        EulerAngles = new Vector3(4f, -15f, -9f)
                    }
                }
            };
        }

        private FrameSceneBuilder CreateBuilder(
            Camera camera,
            Renderer backgroundRenderer,
            Light pointLight,
            PokemonPrefabCatalog.Entry pikachu,
            PokemonPrefabCatalog.Entry charmander,
            PokemonPrefabCatalog.Entry squirtle,
            params Texture2D[] textures)
        {
            return new FrameSceneBuilder(
                camera,
                backgroundRenderer,
                pointLight,
                new PokemonPrefabCatalog(pikachu, charmander, squirtle),
                new BackgroundCatalog(textures),
                2f,
                restoreAutomaticCameraAspect: false);
        }

        private Camera CreateCamera(Vector3 position)
        {
            var cameraObject = Track(new GameObject("Test Camera"));
            cameraObject.transform.position = position;
            cameraObject.transform.rotation = Quaternion.identity;
            return cameraObject.AddComponent<Camera>();
        }

        private Renderer CreateBackgroundRenderer()
        {
            var background = Track(GameObject.CreatePrimitive(PrimitiveType.Quad));
            background.name = "Test Background";
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            materialsToDestroy[materialCount++] = material;
            var renderer = background.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private Light CreatePointLight()
        {
            var lightObject = Track(new GameObject("Test Point Light"));
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.useColorTemperature = true;
            return light;
        }

        private GameObject CreateLabeledCube(string name, PokemonClass pokemonClass)
        {
            var gameObject = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            gameObject.name = name;
            SetLabel(gameObject.AddComponent<PokemonLabel>(), pokemonClass);
            return gameObject;
        }

        private GameObject CreateUnreadableLabeledPrefab(string name, PokemonClass pokemonClass)
        {
            var gameObject = Track(new GameObject(name));
            var meshFilter = gameObject.AddComponent<MeshFilter>();
            gameObject.AddComponent<MeshRenderer>();
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up } };
            mesh.UploadMeshData(true);
            meshFilter.sharedMesh = mesh;
            SetLabel(gameObject.AddComponent<PokemonLabel>(), pokemonClass);
            return gameObject;
        }

        private Texture2D CreateTexture(string name)
        {
            var texture = new Texture2D(2, 2) { name = name };
            texturesToDestroy[textureCount++] = texture;
            return texture;
        }

        private static void SetLabel(PokemonLabel label, PokemonClass pokemonClass)
        {
            var serialized = new SerializedObject(label);
            serialized.FindProperty("pokemonClass").enumValueIndex = (int)pokemonClass;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private GameObject Track(GameObject gameObject)
        {
            objectsToDestroy[objectCount++] = gameObject;
            return gameObject;
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That((actual - expected).sqrMagnitude, Is.LessThan(0.000001f));
        }

        private static void AssertVector(Vector4 actual, Vector4 expected)
        {
            Assert.That((actual - expected).sqrMagnitude, Is.LessThan(0.000001f));
        }

        private static void AssertQuaternion(Quaternion actual, Quaternion expected)
        {
            Assert.That(Quaternion.Angle(actual, expected), Is.LessThan(0.01f));
        }

        private static Vector3 GetExpectedWorldPosition(
            Camera camera,
            Vector2 center,
            float distance,
            float outputAspect)
        {
            var halfHeight = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var cameraDirection = new Vector3(
                (center.x * 2f - 1f) * halfHeight * outputAspect,
                (center.y * 2f - 1f) * halfHeight,
                1f).normalized;
            return camera.transform.position +
                camera.transform.TransformDirection(cameraDirection) * distance;
        }
    }
}
