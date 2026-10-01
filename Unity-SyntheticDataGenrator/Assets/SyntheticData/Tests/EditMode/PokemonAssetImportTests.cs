using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SyntheticData.Tests
{
    public sealed class PokemonAssetImportTests
    {
        [TestCase("pikachu")]
        [TestCase("charmander")]
        [TestCase("squirtle")]
        public void GlbModelImportsAsGameObject(string modelName)
        {
            var path = $"Assets/Data/Models/Pokemon/{modelName}.glb";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            Assert.That(model, Is.Not.Null, $"GLB failed to import: {path}");
            Assert.That(model.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
        }
    }
}
