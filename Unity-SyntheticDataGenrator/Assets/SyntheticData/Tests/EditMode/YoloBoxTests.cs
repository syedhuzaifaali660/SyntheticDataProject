using System.Globalization;
using NUnit.Framework;
using SyntheticData.Labels;
using SyntheticData.Output;

namespace SyntheticData.Tests
{
    public sealed class YoloBoxTests
    {
        [Test]
        public void ConvertsPixelBoundsToNormalizedYoloBox()
        {
            var bounds = new PixelBounds(160f, 160f, 480f, 480f);
            var box = bounds.ToYolo(classId: 2, width: 640, height: 640);

            Assert.That(box.ClassId, Is.EqualTo(2));
            Assert.That(box.CenterX, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(box.CenterY, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(box.Width, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(box.Height, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void ConvertsBottomLeftPixelYToTopLeftYoloCenterY()
        {
            var bounds = new PixelBounds(64f, 32f, 192f, 96f);
            var box = bounds.ToYolo(classId: 1, width: 256, height: 320);

            Assert.That(box.CenterX, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(box.CenterY, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(box.Width, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(box.Height, Is.EqualTo(0.2f).Within(0.0001f));
        }

        [Test]
        public void ClampsPartiallyCroppedBoundsToImage()
        {
            var clamped = new PixelBounds(-10f, 20f, 650f, 500f).Clamp(640, 640);

            Assert.That(clamped.MinX, Is.EqualTo(0f));
            Assert.That(clamped.MaxX, Is.EqualTo(640f));
        }

        [Test]
        public void KeepsImageEdgeBoxValidAfterYoloFormatting()
        {
            var bounds = new PixelBounds(504.01114f, 244.1877f, 640f, 424.7481f);

            var box = bounds.ToYolo(classId: 2, width: 640, height: 640);
            var fields = YoloLabelFormatter.Format(new[] { box }).Trim().Split(' ');
            var centerX = float.Parse(fields[1], CultureInfo.InvariantCulture);
            var width = float.Parse(fields[3], CultureInfo.InvariantCulture);

            Assert.That(box.CenterX + box.Width * 0.5f, Is.LessThanOrEqualTo(1f));
            Assert.That(centerX + width * 0.5f, Is.LessThanOrEqualTo(1f));
        }
    }
}
