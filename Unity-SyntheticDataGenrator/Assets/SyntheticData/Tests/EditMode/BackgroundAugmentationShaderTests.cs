using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SyntheticData.Tests
{
    public sealed class BackgroundAugmentationShaderTests
    {
        [Test]
        public void ProductionBackgroundMaterialConsumesEverySampledAugmentationProperty()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/SyntheticData/Materials/BackgroundCapture.mat");

            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("SyntheticData/BackgroundAugmentation"));
            Assert.That(material.HasProperty("_BaseMap"), Is.True);
            Assert.That(material.HasProperty("_Brightness"), Is.True);
            Assert.That(material.HasProperty("_Contrast"), Is.True);
            Assert.That(material.HasProperty("_Blur"), Is.True);
            Assert.That(material.HasProperty("_Temperature"), Is.True);
        }
    }
}
