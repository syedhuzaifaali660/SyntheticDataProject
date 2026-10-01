using NUnit.Framework;
using SyntheticData.Labels;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace SyntheticData.Tests
{
    public sealed class MeshBoundsProjectorTests
    {
        private GameObject cameraObject;
        private GameObject cube;

        [TearDown]
        public void TearDown()
        {
            if (cameraObject != null)
            {
                var camera = cameraObject.GetComponent<Camera>();
                if (camera != null && camera.targetTexture != null)
                {
                    var targetTexture = camera.targetTexture;
                    camera.targetTexture = null;
                    targetTexture.Release();
                    Object.DestroyImmediate(targetTexture);
                }
            }

            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(cube);
        }

        [Test]
        public void ProjectsProceduralCubeToCenteredNonEmptyImageBounds()
        {
            var camera = CreateCamera();
            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var projector = new MeshBoundsProjector(24f, 0.25f);

            var projected = projector.TryProject(camera, cube, 640, 640, out var bounds);

            Assert.That(projected, Is.True);
            Assert.That(bounds.Width, Is.GreaterThan(0f));
            Assert.That(bounds.Height, Is.GreaterThan(0f));
            Assert.That(bounds.MinX, Is.GreaterThanOrEqualTo(0f));
            Assert.That(bounds.MinY, Is.GreaterThanOrEqualTo(0f));
            Assert.That(bounds.MaxX, Is.LessThanOrEqualTo(640f));
            Assert.That(bounds.MaxY, Is.LessThanOrEqualTo(640f));
            Assert.That((bounds.MinX + bounds.MaxX) / 1280f, Is.EqualTo(0.5f).Within(0.05f));
            Assert.That((bounds.MinY + bounds.MaxY) / 1280f, Is.EqualTo(0.5f).Within(0.05f));
        }

        [Test]
        public void ReturnsIdenticalBoundsAcrossUnchangedConsecutiveCalls()
        {
            var camera = CreateCamera();
            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var projector = new MeshBoundsProjector(24f, 0.25f);

            Assert.That(projector.TryProject(camera, cube, 640, 640, out var first), Is.True);
            Assert.That(projector.TryProject(camera, cube, 640, 640, out var second), Is.True);

            Assert.That(second.MinX, Is.EqualTo(first.MinX));
            Assert.That(second.MinY, Is.EqualTo(first.MinY));
            Assert.That(second.MaxX, Is.EqualTo(first.MaxX));
            Assert.That(second.MaxY, Is.EqualTo(first.MaxY));
        }

        [Test]
        public void RejectsBoxBelowConfiguredMinimumPixels()
        {
            var camera = CreateCamera();
            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var projector = new MeshBoundsProjector(500f, 0.25f);

            Assert.That(projector.TryProject(camera, cube, 640, 640, out _), Is.False);
        }

        [Test]
        public void ProjectsForRequestedNonSquareOutputInsteadOfCameraTargetPixels()
        {
            var camera = CreateCamera(targetWidth: 1000, targetHeight: 1000);
            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var projector = new MeshBoundsProjector(24f, 0.25f);

            Assert.That(projector.TryProject(camera, cube, 800, 400, out var bounds), Is.True);
            Assert.That((bounds.MinX + bounds.MaxX) / 1600f, Is.EqualTo(0.5f).Within(0.05f));
            Assert.That((bounds.MinY + bounds.MaxY) / 800f, Is.EqualTo(0.5f).Within(0.05f));
        }

        [Test]
        public void RejectsPartiallyCroppedCubeAboveConfiguredCropFraction()
        {
            var camera = CreateCamera();
            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = new Vector3(3f, 0f, 0f);
            var projector = new MeshBoundsProjector(24f, 0.25f);

            Assert.That(projector.TryProject(camera, cube, 640, 640, out _), Is.False);
        }

        [Test]
        public void PikachuPrefabCanBeCachedAndProjectedWithGltfFastMeshData()
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
            Assert.That(defines.Split(';'), Does.Contain("GLTFAST_KEEP_MESH_DATA"));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SyntheticData/Prefabs/Pikachu.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var meshFilter = instance.GetComponentInChildren<MeshFilter>(true);
                Assert.That(meshFilter, Is.Not.Null);
                Assert.That(meshFilter.sharedMesh.isReadable, Is.True);

                var cache = MeshVertexCache.Capture(instance);
                Assert.That(cache.Entries, Is.Not.Empty);

                var camera = CreateCamera();
                var projector = new MeshBoundsProjector(24f, 0.25f);
                Assert.That(projector.TryProject(camera, instance, 640, 640, out _), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [TestCase("Pikachu")]
        [TestCase("Charmander")]
        [TestCase("Squirtle")]
        public void ProductionPrefabProjectsAtBaselineCameraDistance(string prefabName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/SyntheticData/Prefabs/{prefabName}.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var camera = CreateCamera();
                var projector = new MeshBoundsProjector(24f, 0.25f);

                Assert.That(
                    projector.TryProject(camera, instance, 640, 640, out var bounds),
                    Is.True,
                    $"{prefabName} must project from its visible skinned-mesh geometry.");
                Assert.That(bounds.Width, Is.GreaterThanOrEqualTo(24f));
                Assert.That(bounds.Height, Is.GreaterThanOrEqualTo(24f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private Camera CreateCamera(int targetWidth = 640, int targetHeight = 640)
        {
            cameraObject = new GameObject("Projection Test Camera");
            cameraObject.transform.position = new Vector3(0f, 0f, -5f);
            cameraObject.transform.rotation = Quaternion.identity;
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.aspect = targetWidth / (float)targetHeight;
            camera.pixelRect = new Rect(0f, 0f, targetWidth, targetHeight);
            camera.targetTexture = new RenderTexture(targetWidth, targetHeight, 16);
            return camera;
        }
    }
}
