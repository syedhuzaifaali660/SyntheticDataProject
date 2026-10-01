using UnityEngine;

namespace SyntheticData.Capture
{
    public interface ICameraCaptureService
    {
        byte[] CapturePng(Camera camera, int width, int height);
    }
}
