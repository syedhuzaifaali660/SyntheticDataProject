using System.Collections.Generic;
using SyntheticData.Domain;
using SyntheticData.Labels;
using UnityEngine;

namespace SyntheticData.Validation
{
    /// <summary>
    /// Draws the most recently accepted YOLO boxes in the Editor Game view.
    /// OnGUI runs outside CameraCaptureService's explicit camera render, so it is never encoded in RGB output.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoundsOverlay : MonoBehaviour
    {
        private readonly List<OverlayEntry> entries = new List<OverlayEntry>();
        private GUIStyle labelStyle;

        public void Show(IReadOnlyList<YoloBox> boxes)
        {
            entries.Clear();
            if (boxes == null)
            {
                return;
            }

            foreach (var box in boxes)
            {
                if (!System.Enum.IsDefined(typeof(PokemonClass), box.ClassId))
                {
                    continue;
                }

                entries.Add(new OverlayEntry((PokemonClass)box.ClassId, box));
            }
        }

        public void Clear()
        {
            entries.Clear();
        }

        public static Color GetColor(PokemonClass pokemonClass)
        {
            switch (pokemonClass)
            {
                case PokemonClass.Pikachu:
                    return new Color(1f, 0.9215686f, 0.0156863f, 1f);
                case PokemonClass.Charmander:
                    return new Color(1f, 0.5019608f, 0f, 1f);
                case PokemonClass.Squirtle:
                    return new Color(0.1294118f, 0.5882353f, 0.9529412f, 1f);
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(pokemonClass));
            }
        }

        public static string FormatLabel(PokemonClass pokemonClass, YoloBox box)
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0} {1:F3} {2:F3} {3:F3} {4:F3}",
                pokemonClass.ToString().ToLowerInvariant(),
                box.CenterX,
                box.CenterY,
                box.Width,
                box.Height);
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!Application.isEditor || entries.Count == 0)
            {
                return;
            }

            labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold
            };

            foreach (var entry in entries)
            {
                Draw(entry);
            }
        }

        private void Draw(OverlayEntry entry)
        {
            var box = entry.Box;
            var rect = new Rect(
                (box.CenterX - (box.Width * 0.5f)) * Screen.width,
                (box.CenterY - (box.Height * 0.5f)) * Screen.height,
                box.Width * Screen.width,
                box.Height * Screen.height);
            var previousColor = GUI.color;
            GUI.color = GetColor(entry.PokemonClass);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - 2f, rect.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, 2f, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 2f, rect.yMin, 2f, rect.height), Texture2D.whiteTexture);
            GUI.Label(rect, FormatLabel(entry.PokemonClass, box), labelStyle);
            GUI.color = previousColor;
        }
#endif

        private readonly struct OverlayEntry
        {
            public OverlayEntry(PokemonClass pokemonClass, YoloBox box)
            {
                PokemonClass = pokemonClass;
                Box = box;
            }

            public PokemonClass PokemonClass { get; }
            public YoloBox Box { get; }
        }
    }
}
