using UnityEngine;

namespace SyntheticData.Labels
{
    /// <summary>
    /// Pixel bounds whose Y axis uses Unity's bottom-left screen origin.
    /// </summary>
    public readonly struct PixelBounds
    {
        private const float NormalizedEdgeInset = 0.000001f;

        public float MinX { get; }
        public float MinY { get; }
        public float MaxX { get; }
        public float MaxY { get; }
        public float Width => MaxX - MinX;
        public float Height => MaxY - MinY;

        public PixelBounds(float minX, float minY, float maxX, float maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public PixelBounds Clamp(int width, int height)
        {
            return new PixelBounds(
                Mathf.Clamp(MinX, 0f, width),
                Mathf.Clamp(MinY, 0f, height),
                Mathf.Clamp(MaxX, 0f, width),
                Mathf.Clamp(MaxY, 0f, height));
        }

        /// <summary>
        /// Converts bottom-left Unity pixels to YOLO's top-left image coordinates.
        /// </summary>
        public YoloBox ToYolo(int classId, int width, int height)
        {
            var minimumX = Mathf.Clamp(MinX / width, NormalizedEdgeInset, 1f - NormalizedEdgeInset);
            var maximumX = Mathf.Clamp(MaxX / width, NormalizedEdgeInset, 1f - NormalizedEdgeInset);
            var minimumY = Mathf.Clamp(MinY / height, NormalizedEdgeInset, 1f - NormalizedEdgeInset);
            var maximumY = Mathf.Clamp(MaxY / height, NormalizedEdgeInset, 1f - NormalizedEdgeInset);
            var centerX = (minimumX + maximumX) * 0.5f;
            var centerY = 1f - (minimumY + maximumY) * 0.5f;
            return new YoloBox(
                classId,
                centerX,
                centerY,
                maximumX - minimumX,
                maximumY - minimumY);
        }
    }
}
