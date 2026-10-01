using UnityEngine;

namespace SyntheticData.Domain
{
    [DisallowMultipleComponent]
    public sealed class PokemonLabel : MonoBehaviour
    {
        [SerializeField] private PokemonClass pokemonClass;

        public PokemonClass PokemonClass => pokemonClass;
        public int ClassId => (int)pokemonClass;
        public string ClassName => pokemonClass.ToString().ToLowerInvariant();
    }
}
