using UnityEngine;

namespace SyntheticData.Labels
{
    public interface IBoundsProjector
    {
        bool TryProject(Camera camera, GameObject target, int width, int height, out PixelBounds bounds);
    }
}
