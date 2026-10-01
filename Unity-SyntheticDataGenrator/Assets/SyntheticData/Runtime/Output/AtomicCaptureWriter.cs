using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace SyntheticData.Output
{
    public sealed class AtomicCaptureWriter
    {
        private static readonly Encoding Utf8WithoutByteOrderMark = new UTF8Encoding(false);
        private readonly string runDirectory;
        private readonly Action<string, byte[]> writeImageBytes;
        private readonly Action<string, string> appendManifest;

        public AtomicCaptureWriter(
            string runDirectory,
            Action<string, byte[]> imageWriter = null,
            Action<string, string> manifestAppender = null)
        {
            if (string.IsNullOrWhiteSpace(runDirectory))
            {
                throw new ArgumentException("A run directory is required.", nameof(runDirectory));
            }

            this.runDirectory = runDirectory;
            writeImageBytes = imageWriter ?? WriteBytesAndFlush;
            appendManifest = manifestAppender ?? AppendManifest;
        }

        public void Write(FrameArtifact artifact)
        {
            CapturePaths paths = null;
            var imageTemporaryWasAvailable = false;
            var labelTemporaryWasAvailable = false;
            var imageFinalCreated = false;
            var labelFinalCreated = false;
            try
            {
                ValidateArtifact(artifact);
                paths = new CapturePaths(runDirectory, artifact.FrameId);
                Directory.CreateDirectory(paths.ImagesDirectory);
                Directory.CreateDirectory(paths.LabelsDirectory);
                EnsureRunConfig(paths, artifact.RunConfigJson);

                imageTemporaryWasAvailable = EnsurePathIsAvailable(paths.ImageTemporaryPath);
                writeImageBytes(paths.ImageTemporaryPath, artifact.ImageBytes);

                labelTemporaryWasAvailable = EnsurePathIsAvailable(paths.LabelTemporaryPath);
                WriteTextAndFlush(paths.LabelTemporaryPath, YoloLabelFormatter.Format(artifact.Boxes));

                File.Move(paths.ImageTemporaryPath, paths.ImagePath);
                imageFinalCreated = true;
                File.Move(paths.LabelTemporaryPath, paths.LabelPath);
                labelFinalCreated = true;

                appendManifest(paths.ManifestPath, JsonUtility.ToJson(artifact.Manifest));
            }
            catch (Exception exception)
            {
                var cleanupFailures = Cleanup(
                    paths,
                    imageTemporaryWasAvailable,
                    labelTemporaryWasAvailable,
                    imageFinalCreated,
                    labelFinalCreated);
                throw CreateWriteException(artifact, exception, cleanupFailures);
            }
        }

        private static void ValidateArtifact(FrameArtifact artifact)
        {
            if (artifact == null)
            {
                throw new ArgumentNullException(nameof(artifact));
            }

            if (artifact.ImageBytes == null)
            {
                throw new ArgumentException("Frame image bytes are required.", nameof(artifact));
            }

            if (artifact.Boxes == null)
            {
                throw new ArgumentException("Frame YOLO boxes are required.", nameof(artifact));
            }

            if (artifact.Manifest == null)
            {
                throw new ArgumentException("Frame manifest is required.", nameof(artifact));
            }

            if (artifact.Manifest.frame_id != artifact.FrameId)
            {
                throw new ArgumentException("Frame ID must match the manifest frame ID.", nameof(artifact));
            }

            if (string.IsNullOrEmpty(artifact.RunConfigJson))
            {
                throw new ArgumentException("Run configuration JSON is required.", nameof(artifact));
            }
        }

        private static bool EnsurePathIsAvailable(string path)
        {
            if (File.Exists(path))
            {
                throw new IOException("Temporary capture path already exists: " + path);
            }

            return true;
        }

        private static void EnsureRunConfig(CapturePaths paths, string runConfigJson)
        {
            if (File.Exists(paths.RunConfigPath))
            {
                var existing = File.ReadAllText(paths.RunConfigPath, Utf8WithoutByteOrderMark);
                if (!string.Equals(existing, runConfigJson, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Run configuration differs from the existing run configuration.");
                }

                return;
            }

            var temporaryPath = paths.RunConfigPath + ".tmp";
            if (!EnsurePathIsAvailable(temporaryPath))
            {
                return;
            }

            try
            {
                WriteTextAndFlush(temporaryPath, runConfigJson);
                File.Move(temporaryPath, paths.RunConfigPath);
            }
            catch
            {
                TryDeleteIfCreated(temporaryPath, true, new StringBuilder());
                throw;
            }
        }

        private static void AppendManifest(string manifestPath, string manifestJson)
        {
            using (var stream = new FileStream(
                       manifestPath,
                       FileMode.OpenOrCreate,
                       FileAccess.ReadWrite,
                       FileShare.None))
            {
                var originalLength = stream.Length;
                try
                {
                    stream.Seek(0, SeekOrigin.End);
                    var bytes = Utf8WithoutByteOrderMark.GetBytes(manifestJson + "\n");
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                catch
                {
                    stream.SetLength(originalLength);
                    stream.Flush(true);
                    throw;
                }
            }
        }

        private static void WriteBytesAndFlush(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private static void WriteTextAndFlush(string path, string text)
        {
            WriteBytesAndFlush(path, Utf8WithoutByteOrderMark.GetBytes(text));
        }

        private static string Cleanup(
            CapturePaths paths,
            bool imageTemporaryWasAvailable,
            bool labelTemporaryWasAvailable,
            bool imageFinalCreated,
            bool labelFinalCreated)
        {
            if (paths == null)
            {
                return null;
            }

            var failures = new StringBuilder();
            TryDeleteIfCreated(paths.ImageTemporaryPath, imageTemporaryWasAvailable, failures);
            TryDeleteIfCreated(paths.LabelTemporaryPath, labelTemporaryWasAvailable, failures);
            TryDeleteIfCreated(paths.ImagePath, imageFinalCreated, failures);
            TryDeleteIfCreated(paths.LabelPath, labelFinalCreated, failures);
            return failures.Length == 0 ? null : failures.ToString();
        }

        private static IOException CreateWriteException(
            FrameArtifact artifact,
            Exception primaryFailure,
            string cleanupFailures)
        {
            var frameId = artifact == null ? "unknown" : artifact.FrameId.ToString();
            var message = "Failed to write frame " + frameId + ". " + primaryFailure.Message;
            if (!string.IsNullOrEmpty(cleanupFailures))
            {
                message += " Cleanup failures: " + cleanupFailures;
            }

            return new IOException(message, primaryFailure);
        }

        private static void TryDeleteIfCreated(string path, bool wasAvailableBeforeWrite, StringBuilder failures)
        {
            if (!wasAvailableBeforeWrite || !File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception exception)
            {
                if (failures.Length > 0)
                {
                    failures.Append(" | ");
                }

                failures.Append(path);
                failures.Append(": ");
                failures.Append(exception.Message);
            }
        }
    }
}
