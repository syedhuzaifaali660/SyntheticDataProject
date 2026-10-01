using NUnit.Framework;
using SyntheticData.Domain;
using SyntheticData.Labels;
using SyntheticData.Validation;
using UnityEngine;

namespace SyntheticData.Tests
{
    public sealed class BoundsOverlayTests
    {
        [TestCase(PokemonClass.Pikachu, 1f, 0.9215686f, 0.0156863f)]
        [TestCase(PokemonClass.Charmander, 1f, 0.5019608f, 0f)]
        [TestCase(PokemonClass.Squirtle, 0.1294118f, 0.5882353f, 0.9529412f)]
        public void UsesTheExpectedClassColor(PokemonClass pokemonClass, float red, float green, float blue)
        {
            var color = BoundsOverlay.GetColor(pokemonClass);

            Assert.That(color.r, Is.EqualTo(red).Within(0.0001f));
            Assert.That(color.g, Is.EqualTo(green).Within(0.0001f));
            Assert.That(color.b, Is.EqualTo(blue).Within(0.0001f));
        }

        [Test]
        public void FormatsClassNameAndNormalizedYoloValues()
        {
            var text = BoundsOverlay.FormatLabel(
                PokemonClass.Squirtle,
                new YoloBox(2, 0.125f, 0.5f, 0.25f, 0.375f));

            Assert.That(text, Is.EqualTo("squirtle 0.125 0.500 0.250 0.375"));
        }
    }
}
