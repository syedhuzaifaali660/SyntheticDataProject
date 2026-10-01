using UnityEngine;

namespace SyntheticData.Labels
{
    public sealed class MeshBoundsProjector : IBoundsProjector
    {
        private readonly float minimumBoxPixels;
        private readonly float maximumCropFraction;
        public MeshBoundsProjector(float minimumBoxPixels, float maximumCropFraction)
        {
            this.minimumBoxPixels = minimumBoxPixels;
            this.maximumCropFraction = maximumCropFraction;
        }

        public bool TryProject(
            Camera camera,
            GameObject target,
            int width,
            int height,
            out PixelBounds bounds)
        {
            bounds = default;
            if (camera == null || target == null || width <= 0 || height <= 0 ||
                !IsFinite(minimumBoxPixels) || !IsFinite(maximumCropFraction) ||
                minimumBoxPixels <= 0f || maximumCropFraction < 0f ||
                maximumCropFraction > 1f)
            {
                return false;
            }

            var cache = MeshVertexCache.GetOrCreate(target);
            var worldToCamera = camera.worldToCameraMatrix;
            var outputProjection = CreateOutputProjection(camera, width / (float)height);
            var worldToOutputClip = outputProjection * worldToCamera;
            var minX = float.PositiveInfinity;
            var minY = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxY = float.NegativeInfinity;
            var foundVertex = false;

            foreach (var entry in cache.Entries)
            {
                if (entry.Transform == null)
                {
                    continue;
                }

                foreach (var localVertex in entry.LocalVertices)
                {
                    var world = TransformVertex(entry, localVertex);
                    var cameraSpace = worldToCamera.MultiplyPoint(world);
                    var depth = -cameraSpace.z;
                    if (depth <= camera.nearClipPlane)
                    {
                        continue;
                    }

                    var clip = worldToOutputClip * new Vector4(world.x, world.y, world.z, 1f);
                    if (!IsFinite(clip.x) || !IsFinite(clip.y) || !IsFinite(clip.w) ||
                        Mathf.Approximately(clip.w, 0f))
                    {
                        continue;
                    }

                    var screenX = ((clip.x / clip.w) + 1f) * 0.5f * width;
                    var screenY = ((clip.y / clip.w) + 1f) * 0.5f * height;
                    minX = Mathf.Min(minX, screenX);
                    minY = Mathf.Min(minY, screenY);
                    maxX = Mathf.Max(maxX, screenX);
                    maxY = Mathf.Max(maxY, screenY);
                    foundVertex = true;
                }
            }

            if (!foundVertex || !IsFinite(minX) || !IsFinite(minY) ||
                !IsFinite(maxX) || !IsFinite(maxY))
            {
                return false;
            }

            var rawBounds = new PixelBounds(minX, minY, maxX, maxY);
            var rawArea = rawBounds.Width * rawBounds.Height;
            if (!IsFinite(rawBounds.Width) || !IsFinite(rawBounds.Height) || rawArea <= 0f ||
                !IsFinite(rawArea))
            {
                return false;
            }

            var clampedBounds = rawBounds.Clamp(width, height);
            var clampedArea = clampedBounds.Width * clampedBounds.Height;
            var cropFraction = 1f - (clampedArea / rawArea);
            if (clampedBounds.Width < minimumBoxPixels ||
                clampedBounds.Height < minimumBoxPixels ||
                !IsFinite(clampedArea) || !IsFinite(cropFraction) ||
                cropFraction > maximumCropFraction)
            {
                return false;
            }

            var normalized = clampedBounds.ToYolo(0, width, height);
            if (!IsValidNormalized(normalized))
            {
                return false;
            }

            bounds = clampedBounds;
            return true;
        }

        private static Vector3 TransformVertex(MeshVertexCache.Entry entry, Vector3 localVertex)
        {
            if (!entry.VerticesIncludeTransformScale)
            {
                return entry.Transform.TransformPoint(localVertex);
            }

            return Matrix4x4.TRS(
                    entry.Transform.position,
                    entry.Transform.rotation,
                    Vector3.one)
                .MultiplyPoint3x4(localVertex);
        }

        private static bool IsValidNormalized(YoloBox box)
        {
            return IsFinite(box.CenterX) && IsFinite(box.CenterY) &&
                IsFinite(box.Width) && IsFinite(box.Height) &&
                box.CenterX >= 0f && box.CenterX <= 1f &&
                box.CenterY >= 0f && box.CenterY <= 1f &&
                box.Width >= 0f && box.Width <= 1f &&
                box.Height >= 0f && box.Height <= 1f;
        }

        private static Matrix4x4 CreateOutputProjection(Camera camera, float outputAspect)
        {
            var projection = camera.projectionMatrix;
            projection.m00 = Mathf.Sign(projection.m00) * Mathf.Abs(projection.m11) / outputAspect;
            return projection;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
