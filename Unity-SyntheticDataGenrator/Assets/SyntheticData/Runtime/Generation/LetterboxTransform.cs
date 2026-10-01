using System;
using UnityEngine;

namespace SyntheticData.Generation
{
    public readonly struct LetterboxTransform
    {
        private LetterboxTransform(float sourceWidth, float sourceHeight, float targetDimension, float scale, float paddingX, float paddingY)
        {
            SourceWidth = sourceWidth;
            SourceHeight = sourceHeight;
            TargetDimension = targetDimension;
            Scale = scale;
            PaddingX = paddingX;
            PaddingY = paddingY;
        }

        public float SourceWidth { get; }
        public float SourceHeight { get; }
        public float TargetDimension { get; }
        public float Scale { get; }
        public float PaddingX { get; }
        public float PaddingY { get; }

        public static LetterboxTransform Create(int sourceWidth, int sourceHeight, int targetDimension)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0 || targetDimension <= 0)
                throw new ArgumentOutOfRangeException(nameof(sourceWidth), "Source dimensions and target dimension must be positive.");
            var scale = Mathf.Min(targetDimension / (float)sourceWidth, targetDimension / (float)sourceHeight);
            return new LetterboxTransform(sourceWidth, sourceHeight, targetDimension, scale,
                (targetDimension - sourceWidth * scale) * 0.5f,
                (targetDimension - sourceHeight * scale) * 0.5f);
        }

        public Rect SourceToModel(Rect sourceNormalized)
        {
            return new Rect((sourceNormalized.x * SourceWidth * Scale + PaddingX) / TargetDimension,
                (sourceNormalized.y * SourceHeight * Scale + PaddingY) / TargetDimension,
                sourceNormalized.width * SourceWidth * Scale / TargetDimension,
                sourceNormalized.height * SourceHeight * Scale / TargetDimension);
        }

        public Rect ModelToSource(Rect modelNormalized)
        {
            return new Rect((modelNormalized.x * TargetDimension - PaddingX) / (SourceWidth * Scale),
                (modelNormalized.y * TargetDimension - PaddingY) / (SourceHeight * Scale),
                modelNormalized.width * TargetDimension / (SourceWidth * Scale),
                modelNormalized.height * TargetDimension / (SourceHeight * Scale));
        }
    }
}
