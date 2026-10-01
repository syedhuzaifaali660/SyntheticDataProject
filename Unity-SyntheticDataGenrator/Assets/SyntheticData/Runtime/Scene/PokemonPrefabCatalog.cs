using System;
using System.Collections.Generic;
using SyntheticData.Domain;
using UnityEngine;

namespace SyntheticData.Scene
{
    [Serializable]
    public sealed class PokemonPrefabCatalog
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private PokemonClass pokemonClass;
            [SerializeField] private GameObject prefab;

            public PokemonClass PokemonClass => pokemonClass;
            public GameObject Prefab => prefab;

            public Entry(PokemonClass pokemonClass, GameObject prefab)
            {
                this.pokemonClass = pokemonClass;
                this.prefab = prefab;
            }
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public PokemonPrefabCatalog()
        {
        }

        public PokemonPrefabCatalog(params Entry[] entries)
        {
            this.entries = entries == null ? new List<Entry>() : new List<Entry>(entries);
            Validate();
        }

        public GameObject Get(PokemonClass pokemonClass)
        {
            var mapping = ValidateAndCreateMapping();
            return mapping[pokemonClass];
        }

        public void Validate()
        {
            ValidateAndCreateMapping();
        }

        private Dictionary<PokemonClass, GameObject> ValidateAndCreateMapping()
        {
            var mapping = new Dictionary<PokemonClass, GameObject>();
            foreach (var entry in entries)
            {
                if (entry == null || !Enum.IsDefined(typeof(PokemonClass), entry.PokemonClass) ||
                    entry.Prefab == null)
                {
                    throw new InvalidOperationException("Pokemon prefab catalog contains an invalid entry.");
                }

                if (mapping.ContainsKey(entry.PokemonClass))
                {
                    throw new InvalidOperationException(
                        $"Pokemon prefab catalog contains duplicate class '{entry.PokemonClass}'.");
                }

                var label = entry.Prefab.GetComponent<PokemonLabel>();
                if (label == null || label.PokemonClass != entry.PokemonClass)
                {
                    throw new InvalidOperationException(
                        $"Pokemon prefab catalog label does not match class '{entry.PokemonClass}'.");
                }

                mapping.Add(entry.PokemonClass, entry.Prefab);
            }

            foreach (PokemonClass pokemonClass in Enum.GetValues(typeof(PokemonClass)))
            {
                if (!mapping.ContainsKey(pokemonClass))
                {
                    throw new InvalidOperationException(
                        $"Pokemon prefab catalog is missing class '{pokemonClass}'.");
                }
            }

            return mapping;
        }
    }
}
