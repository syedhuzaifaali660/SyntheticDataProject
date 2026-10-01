using System;
using System.Collections.Generic;
using SyntheticData.Labels;
using UnityEngine;

namespace SyntheticData.Output
{
    [Serializable]
    public sealed class ManifestObject
    {
        public int class_id;
        public string class_name;
        public float[] bbox;
        public Vector2 center;
        public float distance;
        public float scale;
        public Vector3 euler_angles;
        public string size_band;
        public float model_input_width;
        public float model_input_height;

        public ManifestObject(
            YoloBox box,
            string className,
            Vector2 normalizedCenter,
            float objectDistance,
            float objectScale,
            Vector3 eulerAngles,
            string sizeBand = null,
            float modelInputWidth = 0f,
            float modelInputHeight = 0f)
        {
            class_id = box.ClassId;
            class_name = className;
            bbox = new[] { box.CenterX, box.CenterY, box.Width, box.Height };
            center = normalizedCenter;
            distance = objectDistance;
            scale = objectScale;
            euler_angles = eulerAngles;
            size_band = sizeBand;
            model_input_width = modelInputWidth;
            model_input_height = modelInputHeight;
        }
    }

    [Serializable]
    public sealed class ManifestRecord
    {
        public string run_id;
        public int frame_id;
        public int seed;
        public string split_hint;
        public string background_id;
        public int width;
        public int height;
        public string capture_profile;
        public int source_width;
        public int source_height;
        public float source_aspect_ratio;
        public float letterbox_scale;
        public float letterbox_pad_x;
        public float letterbox_pad_y;
        public string lighting_band;
        public ManifestObject[] objects;

        public ManifestRecord(
            string runId,
            int frameId,
            int frameSeed,
            string splitHint,
            string backgroundId,
            int imageWidth,
            int imageHeight,
            IReadOnlyList<ManifestObject> manifestObjects,
            string captureProfile = null,
            float letterboxScale = 0f,
            float letterboxPadX = 0f,
            float letterboxPadY = 0f,
            string lightingBand = null)
        {
            run_id = runId;
            frame_id = frameId;
            seed = frameSeed;
            split_hint = splitHint;
            background_id = backgroundId;
            width = imageWidth;
            height = imageHeight;
            capture_profile = captureProfile;
            source_width = imageWidth;
            source_height = imageHeight;
            source_aspect_ratio = imageHeight == 0 ? 0f : imageWidth / (float)imageHeight;
            letterbox_scale = letterboxScale;
            letterbox_pad_x = letterboxPadX;
            letterbox_pad_y = letterboxPadY;
            lighting_band = lightingBand;
            objects = Copy(manifestObjects);
        }

        private static ManifestObject[] Copy(IReadOnlyList<ManifestObject> source)
        {
            if (source == null)
            {
                return Array.Empty<ManifestObject>();
            }

            var copy = new ManifestObject[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                copy[index] = source[index];
            }

            return copy;
        }
    }

    public sealed class FrameArtifact
    {
        public FrameArtifact(
            int frameId,
            byte[] imageBytes,
            IReadOnlyList<YoloBox> boxes,
            ManifestRecord manifest,
            string runConfigJson)
        {
            FrameId = frameId;
            ImageBytes = imageBytes;
            Boxes = boxes;
            Manifest = manifest;
            RunConfigJson = runConfigJson;
        }

        public int FrameId { get; }
        public byte[] ImageBytes { get; }
        public IReadOnlyList<YoloBox> Boxes { get; }
        public ManifestRecord Manifest { get; }
        public string RunConfigJson { get; }
    }
}
