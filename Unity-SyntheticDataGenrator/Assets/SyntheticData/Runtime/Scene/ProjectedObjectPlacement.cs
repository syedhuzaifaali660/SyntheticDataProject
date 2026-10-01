using System;
using System.Collections.Generic;
using SyntheticData.Generation;
using SyntheticData.Labels;
using UnityEngine;

namespace SyntheticData.Scene
{
    /// <summary>Fits the complete rotated mesh in the selected native viewport at its requested model-pixel size.</summary>
    public static class ProjectedObjectPlacement
    {
        public static void Fit(Camera camera, GameObject root, FrameRecipe frame, ObjectRecipe recipe)
        {
            // Capture once, before the root is resized. Store camera-oriented, unit-root-scale vertices.
            // Baking again only after placement avoids stale skinned-mesh scale in the label cache.
            var vertices = new List<Vector3>();
            var minimum = Vector3.positiveInfinity;
            var maximum = Vector3.negativeInfinity;
            foreach (var entry in MeshVertexCache.Capture(root).Entries)
            foreach (var vertex in entry.LocalVertices)
            {
                var world = entry.VerticesIncludeTransformScale
                    ? Matrix4x4.TRS(entry.Transform.position, entry.Transform.rotation, Vector3.one).MultiplyPoint3x4(vertex)
                    : entry.Transform.TransformPoint(vertex);
                var offset = camera.transform.InverseTransformDirection(world - root.transform.position) / recipe.Scale;
                vertices.Add(offset);
                minimum = Vector3.Min(minimum, offset);
                maximum = Vector3.Max(maximum, offset);
            }
            if (vertices.Count == 0) throw new InvalidOperationException("Cannot place an object without mesh vertices.");
            var meshCenter = (minimum + maximum) * .5f;
            for (var i = 0; i < vertices.Count; i++) vertices[i] -= meshCenter;

            var halfY = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
            var halfX = halfY * camera.aspect;
            var modelScale = 640f / Mathf.Max(frame.OutputWidth, frame.OutputHeight);
            var modelWidth = frame.OutputWidth * modelScale;
            var modelHeight = frame.OutputHeight * modelScale;
            var position = new Vector3(0, 0, recipe.Distance);
            var scale = Mathf.Min(recipe.Scale, recipe.Distance * .1f / Mathf.Max((maximum - minimum).magnitude, .001f));
            var sample = new SeededRandomSource(unchecked(frame.Seed + recipe.ClassId * 397));
            var desiredSide = recipe.SizeBand == ObjectSizeBand.Small ? sample.Range(16f, 68f)
                : recipe.SizeBand == ObjectSizeBand.Medium ? sample.Range(80f, 184f) : sample.Range(208f, 432f);
            var profile = frame.CaptureProfile;
            var desiredCenter = recipe.Center;
            var fitted = false;
            for (var iteration = 0; iteration < 64; iteration++)
            {
                var min = Vector2.positiveInfinity;
                var max = Vector2.negativeInfinity;
                foreach (var vertex in vertices)
                {
                    var point = position + vertex * scale;
                    if (point.z <= camera.nearClipPlane)
                        throw new InvalidOperationException("Projected placement crossed the camera near plane.");
                    var projected = new Vector2(.5f + point.x / (2 * halfX * point.z), .5f + point.y / (2 * halfY * point.z));
                    min = Vector2.Min(min, projected);
                    max = Vector2.Max(max, projected);
                }
                var size = max - min;
                var side = Mathf.Max(size.x * modelWidth, size.y * modelHeight);
                // A tall large object on a landscape viewport must fit the shorter image axis too.
                var target = Mathf.Min(desiredSide, side * .92f / Mathf.Max(size.x, size.y));
                var expectedSize = size * (target / side);
                var marginX = Mathf.Max(profile.HorizontalSafeMargin, expectedSize.x * .5f + .025f);
                var marginY = Mathf.Max(profile.VerticalSafeMargin, expectedSize.y * .5f + .025f);
                var center = new Vector2(Mathf.Clamp(desiredCenter.x, marginX, 1 - marginX), Mathf.Clamp(desiredCenter.y, marginY, 1 - marginY));
                var error = center - (min + max) * .5f;
                if (Mathf.Abs(side - target) < .1f && error.sqrMagnitude < .00000025f)
                {
                    fitted = true;
                    recipe.Center = (min + max) * .5f;
                    break;
                }
                scale *= Mathf.Lerp(1f, Mathf.Clamp(target / side, .5f, 2f), .5f);
                position.x += error.x * halfX * position.z;
                position.y += error.y * halfY * position.z;
            }
            if (!fitted) throw new InvalidOperationException($"Projected placement did not converge: frame {frame.FrameIndex + 1}, class {recipe.ClassId}.");
            root.transform.localScale = Vector3.one * scale;
            root.transform.position = camera.transform.TransformPoint(position - meshCenter * scale);
            recipe.Scale = scale;
            recipe.Distance = Vector3.Distance(camera.transform.position, root.transform.position);
        }
    }
}
