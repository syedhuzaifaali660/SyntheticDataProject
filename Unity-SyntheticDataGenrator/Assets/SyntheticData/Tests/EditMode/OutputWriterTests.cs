using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using SyntheticData.Labels;
using SyntheticData.Output;
using UnityEngine;

namespace SyntheticData.Tests
{
    public sealed class OutputWriterTests
    {
        private string temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "SyntheticData-OutputWriterTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }

        [Test]
        public void FormatsOneYoloRecordWithSixDecimalPlaces()
        {
            var text = YoloLabelFormatter.Format(new[]
            {
                new YoloBox(1, 0.5f, 0.25f, 0.125f, 0.75f)
            });

            Assert.That(text, Is.EqualTo("1 0.500000 0.250000 0.125000 0.750000\n"));
        }

        [Test]
        public void FormatsYoloRecordsWithDecimalPointsUnderGermanCulture()
        {
            var originalCulture = CultureInfo.CurrentCulture;
            var originalUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");

                var text = YoloLabelFormatter.Format(new[]
                {
                    new YoloBox(2, 0.5f, 0.25f, 0.125f, 0.75f)
                });

                Assert.That(text, Is.EqualTo("2 0.500000 0.250000 0.125000 0.750000\n"));
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
        }

        [Test]
        public void WriteCreatesTwoPairedCapturesAndIndependentlyParseableManifestRecords()
        {
            var writer = new AtomicCaptureWriter(temporaryDirectory);
            writer.Write(CreateArtifact(frameId: 1, runConfigJson: "{\"seed\":42}"));
            writer.Write(CreateArtifact(frameId: 2, runConfigJson: "{\"seed\":42}"));

            var firstImage = Path.Combine(temporaryDirectory, "images", "frame_000001.png");
            var firstLabel = Path.Combine(temporaryDirectory, "labels", "frame_000001.txt");
            var secondImage = Path.Combine(temporaryDirectory, "images", "frame_000002.png");
            var secondLabel = Path.Combine(temporaryDirectory, "labels", "frame_000002.txt");
            var manifestPath = Path.Combine(temporaryDirectory, "manifest.jsonl");
            var runConfigPath = Path.Combine(temporaryDirectory, "run-config.json");

            Assert.That(File.ReadAllBytes(firstImage), Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(File.ReadAllBytes(secondImage), Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(File.ReadAllText(firstLabel), Is.EqualTo("0 0.500000 0.500000 0.200000 0.400000\n"));
            Assert.That(File.ReadAllText(secondLabel), Is.EqualTo("0 0.500000 0.500000 0.200000 0.400000\n"));
            Assert.That(File.ReadAllText(runConfigPath), Is.EqualTo("{\"seed\":42}"));

            var records = File.ReadAllText(manifestPath).Split(new[] { '\n' }, StringSplitOptions.None);
            Assert.That(records, Has.Length.EqualTo(3));
            Assert.That(records[0], Is.Not.Empty);
            Assert.That(records[1], Is.Not.Empty);
            Assert.That(records[2], Is.Empty);
            AssertManifestRecord(records[0], frameId: 1);
            AssertManifestRecord(records[1], frameId: 2);
        }

        [Test]
        public void ManifestSerializesMixedDeviceCaptureMetadata()
        {
            var manifest = CreateArtifact(frameId: 1, runConfigJson: "{\"seed\":42}").Manifest;

            Assert.That(manifest.capture_profile, Is.EqualTo("IPhonePortrait"));
            Assert.That(manifest.source_width, Is.EqualTo(360));
            Assert.That(manifest.source_height, Is.EqualTo(780));
            Assert.That(manifest.letterbox_scale, Is.EqualTo(640f / 780f).Within(0.0001f));
            Assert.That(manifest.lighting_band, Is.EqualTo("Low"));
            Assert.That(manifest.objects[0].size_band, Is.EqualTo("Small"));
        }

        [Test]
        public void ImageWriteFailureLeavesNoFinalImageOrLabel()
        {
            var writer = new AtomicCaptureWriter(
                temporaryDirectory,
                (path, bytes) => throw new IOException("Injected image write failure."));
            var artifact = CreateArtifact(frameId: 2, runConfigJson: "{\"seed\":42}");

            Assert.That(
                () => writer.Write(artifact),
                Throws.InstanceOf<IOException>().With.Message.Contains("frame 2"));

            var paths = new CapturePaths(temporaryDirectory, 2);
            Assert.That(File.Exists(paths.ImagePath), Is.False);
            Assert.That(File.Exists(paths.LabelPath), Is.False);
            Assert.That(File.Exists(paths.ImageTemporaryPath), Is.False);
            Assert.That(File.Exists(paths.LabelTemporaryPath), Is.False);
        }

        [Test]
        public void FailureDoesNotDeleteAPreexistingFinalImage()
        {
            var paths = new CapturePaths(temporaryDirectory, 3);
            Directory.CreateDirectory(paths.ImagesDirectory);
            File.WriteAllBytes(paths.ImagePath, new byte[] { 9, 9, 9 });
            var writer = new AtomicCaptureWriter(temporaryDirectory);

            Assert.That(
                () => writer.Write(CreateArtifact(frameId: 3, runConfigJson: "{\"seed\":42}")),
                Throws.InstanceOf<IOException>().With.Message.Contains("frame 3"));

            Assert.That(File.ReadAllBytes(paths.ImagePath), Is.EqualTo(new byte[] { 9, 9, 9 }));
            Assert.That(File.Exists(paths.LabelPath), Is.False);
        }

        [Test]
        public void ExistingFinalLabelRollsBackNewImageAndPreservesManifest()
        {
            var writer = new AtomicCaptureWriter(temporaryDirectory);
            writer.Write(CreateArtifact(frameId: 1, runConfigJson: "{\"seed\":42}"));
            var manifestPath = Path.Combine(temporaryDirectory, "manifest.jsonl");
            var originalManifest = File.ReadAllText(manifestPath);
            var paths = new CapturePaths(temporaryDirectory, 2);
            Directory.CreateDirectory(paths.LabelsDirectory);
            File.WriteAllText(paths.LabelPath, "pre-existing label\n");

            Assert.That(
                () => writer.Write(CreateArtifact(frameId: 2, runConfigJson: "{\"seed\":42}")),
                Throws.InstanceOf<IOException>().With.Message.Contains("frame 2"));

            Assert.That(File.Exists(paths.ImagePath), Is.False);
            Assert.That(File.ReadAllText(paths.LabelPath), Is.EqualTo("pre-existing label\n"));
            Assert.That(File.Exists(paths.ImageTemporaryPath), Is.False);
            Assert.That(File.Exists(paths.LabelTemporaryPath), Is.False);
            Assert.That(File.ReadAllText(manifestPath), Is.EqualTo(originalManifest));
        }

        [Test]
        public void ManifestAppendFailureRollsBackBothNewFinalFiles()
        {
            var writer = new AtomicCaptureWriter(
                temporaryDirectory,
                imageWriter: null,
                manifestAppender: (path, json) => throw new IOException("Injected manifest append failure."));
            var paths = new CapturePaths(temporaryDirectory, 1);

            Assert.That(
                () => writer.Write(CreateArtifact(frameId: 1, runConfigJson: "{\"seed\":42}")),
                Throws.InstanceOf<IOException>().With.Message.Contains("frame 1"));

            Assert.That(File.Exists(paths.ImagePath), Is.False);
            Assert.That(File.Exists(paths.LabelPath), Is.False);
            Assert.That(File.Exists(paths.ImageTemporaryPath), Is.False);
            Assert.That(File.Exists(paths.LabelTemporaryPath), Is.False);
            Assert.That(File.Exists(paths.ManifestPath), Is.False);
        }

        [Test]
        public void RejectsDifferentRunConfigurationWithoutWritingAnotherFrame()
        {
            var writer = new AtomicCaptureWriter(temporaryDirectory);
            writer.Write(CreateArtifact(frameId: 1, runConfigJson: "{\"seed\":42}"));

            Assert.That(
                () => writer.Write(CreateArtifact(frameId: 2, runConfigJson: "{\"seed\":43}")),
                Throws.InstanceOf<IOException>().With.Message.Contains("frame 2"));

            var secondPaths = new CapturePaths(temporaryDirectory, 2);
            Assert.That(File.Exists(secondPaths.ImagePath), Is.False);
            Assert.That(File.Exists(secondPaths.LabelPath), Is.False);
        }

        [TestCase(0)]
        [TestCase(1000000)]
        public void RejectsFrameIdsOutsideTheSixDigitRangeWithFrameContext(int frameId)
        {
            var writer = new AtomicCaptureWriter(temporaryDirectory);

            Assert.That(
                () => writer.Write(CreateArtifact(frameId, "{\"seed\":42}")),
                Throws.InstanceOf<IOException>().With.Message.Contains("frame " + frameId));
        }

        private static void AssertManifestRecord(string json, int frameId)
        {
            Assert.That(
                GetObjectPropertyNames(json, objectDepth: 1),
                Is.EquivalentTo(new[]
                {
                    "run_id", "frame_id", "seed", "split_hint", "background_id", "width", "height",
                    "capture_profile", "source_width", "source_height", "source_aspect_ratio",
                    "letterbox_scale", "letterbox_pad_x", "letterbox_pad_y", "lighting_band", "objects"
                }));
            Assert.That(
                GetObjectPropertyNames(json, objectDepth: 2),
                Is.EquivalentTo(new[]
                {
                    "class_id", "class_name", "bbox", "center", "distance", "scale", "euler_angles",
                    "size_band", "model_input_width", "model_input_height"
                }));

            var record = JsonUtility.FromJson<ManifestRecord>(json);
            Assert.That(record.run_id, Is.EqualTo("baseline-20260829-001"));
            Assert.That(record.frame_id, Is.EqualTo(frameId));
            Assert.That(record.seed, Is.EqualTo(42));
            Assert.That(record.split_hint, Is.EqualTo("train"));
            Assert.That(record.background_id, Is.EqualTo("indoor_design_001"));
            Assert.That(record.width, Is.EqualTo(360));
            Assert.That(record.height, Is.EqualTo(780));
            Assert.That(record.capture_profile, Is.EqualTo("IPhonePortrait"));
            Assert.That(record.source_width, Is.EqualTo(360));
            Assert.That(record.source_height, Is.EqualTo(780));
            Assert.That(record.lighting_band, Is.EqualTo("Low"));
            Assert.That(record.objects, Has.Length.EqualTo(1));
            Assert.That(record.objects[0].class_id, Is.EqualTo(0));
            Assert.That(record.objects[0].class_name, Is.EqualTo("pikachu"));
            Assert.That(record.objects[0].bbox, Is.EqualTo(new[] { 0.5f, 0.5f, 0.2f, 0.4f }).Within(0.000001f));
            Assert.That(record.objects[0].center, Is.EqualTo(new Vector2(0.25f, 0.75f)));
            Assert.That(record.objects[0].distance, Is.EqualTo(3f));
            Assert.That(record.objects[0].scale, Is.EqualTo(1.2f).Within(0.000001f));
            Assert.That(record.objects[0].euler_angles, Is.EqualTo(new Vector3(10f, 20f, 30f)));
            Assert.That(record.objects[0].size_band, Is.EqualTo("Small"));
        }

        private static ISet<string> GetObjectPropertyNames(string json, int objectDepth)
        {
            var names = new HashSet<string>();
            var depth = 0;
            for (var index = 0; index < json.Length; index++)
            {
                if (json[index] == '{')
                {
                    depth++;
                    continue;
                }

                if (json[index] == '}')
                {
                    depth--;
                    continue;
                }

                if (json[index] != '"' || depth != objectDepth)
                {
                    continue;
                }

                var keyStart = index + 1;
                var keyEnd = json.IndexOf('"', keyStart);
                if (keyEnd < 0)
                {
                    break;
                }

                var next = keyEnd + 1;
                while (next < json.Length && char.IsWhiteSpace(json[next]))
                {
                    next++;
                }

                if (next < json.Length && json[next] == ':')
                {
                    names.Add(json.Substring(keyStart, keyEnd - keyStart));
                }

                index = keyEnd;
            }

            return names;
        }

        private static FrameArtifact CreateArtifact(int frameId, string runConfigJson)
        {
            var box = new YoloBox(0, 0.5f, 0.5f, 0.2f, 0.4f);
            var manifestObject = new ManifestObject(
                box,
                "pikachu",
                new Vector2(0.25f, 0.75f),
                3f,
                1.2f,
                new Vector3(10f, 20f, 30f),
                sizeBand: "Small",
                modelInputWidth: 100f,
                modelInputHeight: 200f);
            var manifest = new ManifestRecord(
                "baseline-20260829-001",
                frameId,
                42,
                "train",
                "indoor_design_001",
                360,
                780,
                new[] { manifestObject },
                captureProfile: "IPhonePortrait",
                letterboxScale: 640f / 780f,
                letterboxPadX: 172.3077f,
                letterboxPadY: 0f,
                lightingBand: "Low");
            return new FrameArtifact(
                frameId,
                new byte[] { 1, 2, 3 },
                new[] { box },
                manifest,
                runConfigJson);
        }
    }
}
