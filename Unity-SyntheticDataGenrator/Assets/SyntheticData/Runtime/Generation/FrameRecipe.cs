using System;
using System.Collections.Generic;
using UnityEngine;

namespace SyntheticData.Generation
{
    [Serializable]
    public sealed class FrameRecipe
    {
        public int FrameIndex;
        public int AttemptIndex;
        public int Seed;
        public CaptureProfile CaptureProfile;
        public int OutputWidth;
        public int OutputHeight;
        public BackgroundRecipe Background = new BackgroundRecipe();
        public float CameraFov;
        public LightRecipe Light = new LightRecipe();
        public List<ObjectRecipe> Objects = new List<ObjectRecipe>();

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }
    }

    [Serializable]
    public sealed class BackgroundRecipe
    {
        public int Index;
        public Vector2 CropOffset;
        public float Scale;
        public float Brightness;
        public float Contrast;
        public float Blur;
        public float Temperature;
    }

    [Serializable]
    public sealed class ObjectRecipe
    {
        public int ClassId;
        public Vector2 Center;
        public float Distance;
        public float Scale;
        public ObjectSizeBand SizeBand;
        public Vector3 EulerAngles;
    }

    [Serializable]
    public sealed class LightRecipe
    {
        public float AmbientIntensity;
        public float PointIntensity;
        public Vector3 PointPosition;
        public float Temperature;
        public LightingBand Band;
    }
}
