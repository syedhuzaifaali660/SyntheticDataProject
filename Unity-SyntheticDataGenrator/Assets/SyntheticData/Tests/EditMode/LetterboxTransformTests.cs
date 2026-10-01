using System.Collections.Generic;
using NUnit.Framework;
using SyntheticData.Generation;
using UnityEngine;

namespace SyntheticData.Tests
{
    public sealed class LetterboxTransformTests
    {
        [Test]
        public void PortraitFrameUsesHorizontalPaddingAndRoundTripsRectangles()
        {
            var transform = LetterboxTransform.Create(360, 780, 640);
            var source = new Rect(0.15f, 0.2f, 0.5f, 0.4f);

            Assert.That(transform.Scale, Is.EqualTo(640f / 780f).Within(0.0001f));
            Assert.That(transform.PaddingX, Is.GreaterThan(0f));
            Assert.That(transform.PaddingY, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(transform.ModelToSource(transform.SourceToModel(source)),
                Is.EqualTo(source).Using(RectComparer.Within(0.0001f)));
        }

        private sealed class RectComparer : IEqualityComparer<Rect>
        {
            private readonly float tolerance;

            private RectComparer(float tolerance)
            {
                this.tolerance = tolerance;
            }

            public static RectComparer Within(float tolerance)
            {
                return new RectComparer(tolerance);
            }

            public bool Equals(Rect first, Rect second)
            {
                return Mathf.Abs(first.x - second.x) <= tolerance &&
                       Mathf.Abs(first.y - second.y) <= tolerance &&
                       Mathf.Abs(first.width - second.width) <= tolerance &&
                       Mathf.Abs(first.height - second.height) <= tolerance;
            }

            public int GetHashCode(Rect value)
            {
                return value.GetHashCode();
            }
        }
    }
}
