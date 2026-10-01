using System.Collections;
using NUnit.Framework;
using SyntheticData.Capture;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SyntheticData.Tests
{
    public sealed class CaptureSmokeTests
    {
        private GameObject cameraObject;
        private GameObject quad;
        private Material material;

        [UnityTest]
        public IEnumerator CapturePngRendersReadableRedPixelsAndRestoresRenderState()
        {
            var previousActive = RenderTexture.active;
            var existingTarget = new RenderTexture(8, 8, 24);
            var existingActive = new RenderTexture(8, 8, 24);
            Assert.That(existingTarget.Create(), Is.True);
            Assert.That(existingActive.Create(), Is.True);
            cameraObject = new GameObject("Capture Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.transform.position = new Vector3(0f, 0f, -5f);
            camera.orthographic = true;
            camera.orthographicSize = 1f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.targetTexture = existingTarget;
            quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.transform.position = Vector3.zero;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", Color.red);
            quad.GetComponent<Renderer>().material = material;

            yield return null;

            RenderTexture.active = existingActive;
            var captureService = new CameraCaptureService();
            Assert.That(captureService.CapturePng(camera, 64, 64), Is.Not.Empty);
            Assert.That(camera.targetTexture, Is.SameAs(existingTarget));
            Assert.That(RenderTexture.active, Is.SameAs(existingActive));
            yield return null;
            RenderTexture.active = existingActive;
            var renderTextureCountBefore = Resources.FindObjectsOfTypeAll<RenderTexture>().Length;
            var bytes = captureService.CapturePng(camera, 64, 64);
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(decoded.LoadImage(bytes), Is.True);
                Assert.That(decoded.width, Is.EqualTo(64));
                Assert.That(decoded.height, Is.EqualTo(64));
                Assert.That(decoded.GetPixel(32, 32).r, Is.GreaterThan(0.9f));
                Assert.That(camera.targetTexture, Is.SameAs(existingTarget));
                Assert.That(RenderTexture.active, Is.SameAs(existingActive));
                Assert.That(Resources.FindObjectsOfTypeAll<RenderTexture>().Length, Is.EqualTo(renderTextureCountBefore));

                material.SetColor("_BaseColor", new Color(0.5f, 0f, 0f, 1f));
                Assert.That(decoded.LoadImage(captureService.CapturePng(camera, 64, 64)), Is.True);
                Assert.That(decoded.GetPixel(32, 32).r, Is.EqualTo(0.5f).Within(0.06f));
            }
            finally
            {
                Object.Destroy(decoded);
                camera.targetTexture = null;
                RenderTexture.active = previousActive;
                Object.Destroy(existingTarget);
                Object.Destroy(existingActive);
            }
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(quad);
            Object.Destroy(material);
            Object.Destroy(cameraObject);
        }
    }
}
