using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SyntheticData.Scene
{
    [Serializable]
    public sealed class BackgroundCatalog
    {
        [SerializeField] private List<Texture2D> textures = new List<Texture2D>();

        public BackgroundCatalog()
        {
        }

        public BackgroundCatalog(params Texture2D[] textures)
        {
            this.textures = textures == null ? new List<Texture2D>() : new List<Texture2D>(textures);
            Validate();
        }

        public IReadOnlyList<Texture2D> Textures => GetSortedEntries().Select(entry => entry.Texture).ToArray();

        public IReadOnlyList<string> BackgroundIds => GetSortedEntries().Select(entry => entry.Id).ToArray();

        public Texture2D Get(int index)
        {
            var ordered = GetSortedEntries();
            if (index < 0 || index >= ordered.Count)
            {
                throw new InvalidOperationException($"Background index {index} is outside the catalog.");
            }

            return ordered[index].Texture;
        }

        public string GetId(int index)
        {
            var ordered = GetSortedEntries();
            if (index < 0 || index >= ordered.Count)
            {
                throw new InvalidOperationException($"Background index {index} is outside the catalog.");
            }

            return ordered[index].Id;
        }

        public void Validate()
        {
            GetSortedEntries();
        }

        private List<BackgroundEntry> GetSortedEntries()
        {
            if (textures == null || textures.Count == 0)
            {
                throw new InvalidOperationException("Background catalog must contain at least one texture.");
            }

            var entries = new List<BackgroundEntry>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var texture in textures)
            {
                var id = texture == null ? null : Path.GetFileNameWithoutExtension(texture.name);
                if (string.IsNullOrWhiteSpace(id))
                {
                    throw new InvalidOperationException("Background catalog contains an invalid background ID.");
                }

                if (!ids.Add(id))
                {
                    throw new InvalidOperationException(
                        $"Background catalog contains duplicate background ID '{id}'.");
                }

                entries.Add(new BackgroundEntry(id, texture));
            }

            return entries.OrderBy(entry => entry.Id, StringComparer.Ordinal).ToList();
        }

        private readonly struct BackgroundEntry
        {
            public string Id { get; }
            public Texture2D Texture { get; }

            public BackgroundEntry(string id, Texture2D texture)
            {
                Id = id;
                Texture = texture;
            }
        }
    }
}
