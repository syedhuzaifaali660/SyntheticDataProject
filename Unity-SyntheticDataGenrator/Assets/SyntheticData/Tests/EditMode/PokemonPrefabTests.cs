using System.Linq;
using NUnit.Framework;
using SyntheticData.Domain;
using UnityEditor;
using UnityEngine;

namespace SyntheticData.Tests
{
    public sealed class PokemonPrefabTests
    {
        [TestCase("Pikachu", PokemonClass.Pikachu, 0, "pikachu")]
        [TestCase("Charmander", PokemonClass.Charmander, 1, "charmander")]
        [TestCase("Squirtle", PokemonClass.Squirtle, 2, "squirtle")]
        public void PrefabHasStableLabelAndUsableRenderers(
            string name,
            PokemonClass expectedClass,
            int expectedClassId,
            string expectedClassName)
        {
            var prefab = LoadPrefab(name);
            var label = prefab.GetComponent<PokemonLabel>();
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);

            Assert.That(label, Is.Not.Null);
            Assert.That(label.PokemonClass, Is.EqualTo(expectedClass));
            Assert.That(label.ClassId, Is.EqualTo(expectedClassId));
            Assert.That(label.ClassName, Is.EqualTo(expectedClassName));
            Assert.That(renderers, Is.Not.Empty);
            Assert.That(renderers, Is.All.Matches<Renderer>(renderer => renderer.enabled));
            Assert.That(
                renderers.SelectMany(renderer => renderer.sharedMaterials),
                Is.All.Not.Null);
        }

        [TestCase("Pikachu")]
        [TestCase("Charmander")]
        [TestCase("Squirtle")]
        public void PrefabUsesUnitHolderScaleAndIsCenteredAndGrounded(string name)
        {
            var instance = Object.Instantiate(LoadPrefab(name));
            try
            {
                var modelHolder = instance.transform.Find("ModelHolder");
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1))
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                Assert.That(modelHolder, Is.Not.Null);
                Assert.That(
                    Quaternion.Angle(instance.transform.localRotation, Quaternion.identity),
                    Is.LessThan(0.01f),
                    "Recipe rotations replace the prefab root, so facing corrections belong below ModelHolder.");
                Assert.That(modelHolder.Find("Model"), Is.Not.Null);
                Assert.That(modelHolder.localScale.x, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(modelHolder.localScale.y, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(modelHolder.localScale.z, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.01f));
                Assert.That(bounds.center.x, Is.EqualTo(0f).Within(0.01f));
                Assert.That(bounds.center.z, Is.EqualTo(0f).Within(0.01f));
                Assert.That(bounds.size.x, Is.GreaterThan(0f));
                Assert.That(bounds.size.y, Is.GreaterThan(0f));
                Assert.That(bounds.size.z, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static GameObject LoadPrefab(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/SyntheticData/Prefabs/{name}.prefab");
            Assert.That(prefab, Is.Not.Null, $"Missing normalized prefab: {name}");
            return prefab;
        }
    }
}
