using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SyntheticData.Capture
{
    public sealed class CameraCaptureService : ICameraCaptureService
    {
        public byte[] CapturePng(Camera camera, int width, int height)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Capture width must be positive.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "Capture height must be positive.");
            }

            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            RenderTexture temporaryTarget = null;
            Texture2D image = null;
            try
            {
                temporaryTarget = RenderTexture.GetTemporary(
                    width,
                    height,
                    24,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Default);
                camera.targetTexture = temporaryTarget;
                camera.Render();
                RenderTexture.active = temporaryTarget;
                image = new Texture2D(width, height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                image.Apply();
                return image.EncodeToPNG();
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (image != null)
                {
                    Object.Destroy(image);
                }

                if (temporaryTarget != null)
                {
                    RenderTexture.ReleaseTemporary(temporaryTarget);
                }
            }
        }
    }
}
