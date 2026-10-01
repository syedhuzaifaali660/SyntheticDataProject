using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SyntheticData.Capture;
using SyntheticData.Output;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SyntheticData.Editor
{
    [InitializeOnLoad]
    public static class CaptureRunCommand
    {
        private const string CaptureScenePath = "Assets/SyntheticData/Scenes/PokemonCapture.unity";
        private static IEnumerator activeGeneration;
        private static CommandState activeState;

        static CaptureRunCommand()
        {
            EditorApplication.update -= PollRun;
            EditorApplication.update += PollRun;
        }

        public static bool TryParseRequest(
            IReadOnlyList<string> arguments,
            out CaptureRunRequest request,
            out string error)
        {
            request = default;
            if (!TryReadOption(arguments, "-captureRunId", out var runId, out error))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(runId))
            {
                error = "The -captureRunId argument is required.";
                return false;
            }

            if (!TryReadOption(arguments, "-captureFrameCount", out var frameCountText, out error))
            {
                return false;
            }

            if (!int.TryParse(
                    frameCountText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var frameCount) ||
                frameCount <= 0)
            {
                error = "The -captureFrameCount argument must be a positive integer.";
                return false;
            }

            if (!TryReadOption(arguments, "-captureSeed", out var seedText, out error))
            {
                return false;
            }

            var seed = 42;
            if (seedText != null &&
                !int.TryParse(seedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed))
            {
                error = "The -captureSeed argument must be an integer.";
                return false;
            }

            if (!TryReadOption(arguments, "-captureOutputRoot", out var outputRoot, out error))
            {
                return false;
            }

            request = new CaptureRunRequest(
                runId,
                outputRoot ?? CaptureRunPaths.GetDefaultOutputRoot(),
                frameCount,
                seed,
                HasFlag(arguments, "-captureMixedDeviceProfiles"));
            error = CaptureRunRequestValidator.Validate(request);
            return string.IsNullOrEmpty(error);
        }

        public static void Run()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("CaptureRunCommand.Run is only available in batch mode.");
            }

            if (!TryParseRequest(Environment.GetCommandLineArgs(), out var request, out var error))
            {
                throw new ArgumentException(error);
            }

            if (activeGeneration != null)
            {
                throw new InvalidOperationException("Another batch capture command is already active.");
            }

            var runDirectory = RunDirectoryPolicy.Resolve(request.OutputRoot, request.RunId);
            if (Directory.Exists(runDirectory))
            {
                throw new IOException("Capture run directory already exists: " + runDirectory);
            }

            EditorSceneManager.OpenScene(CaptureScenePath, OpenSceneMode.Single);
            var runner = Object.FindFirstObjectByType<CaptureSceneRunner>();
            var configurationError = runner == null
                ? "PokemonCapture requires a CaptureSceneRunner context."
                : runner.ValidateConfiguration();
            if (!string.IsNullOrEmpty(configurationError))
            {
                throw new InvalidOperationException(configurationError);
            }

            activeState = new CommandState(request);
            activeGeneration = runner.CreateRun(
                request.OutputRoot,
                request.RunId,
                request.FrameCount,
                request.Seed,
                request.MixedDeviceProfiles);
            Debug.Log($"Starting synthetic capture command for '{request.RunId}'.");
        }

        private static bool TryReadOption(
            IReadOnlyList<string> arguments,
            string option,
            out string value,
            out string error)
        {
            value = null;
            error = null;
            for (var index = 0; index < arguments.Count; index++)
            {
                if (!string.Equals(arguments[index], option, StringComparison.Ordinal))
                {
                    continue;
                }

                if (index + 1 >= arguments.Count ||
                    string.IsNullOrWhiteSpace(arguments[index + 1]) ||
                    arguments[index + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    error = option + " requires a value.";
                    return false;
                }

                value = arguments[index + 1];
                return true;
            }

            return true;
        }

        private static bool HasFlag(IReadOnlyList<string> arguments, string flag)
        {
            for (var index = 0; index < arguments.Count; index++)
            {
                if (string.Equals(arguments[index], flag, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static void PollRun()
        {
            if (activeGeneration == null || activeState == null)
            {
                return;
            }

            try
            {
                if (activeGeneration.MoveNext())
                {
                    return;
                }

                var completedState = activeState;
                DisposeActiveGeneration();
                VerifyRun(completedState);
                Complete(completedState);
            }
            catch (Exception exception)
            {
                DisposeActiveGeneration();
                Fail(exception);
            }
        }

        private static void DisposeActiveGeneration()
        {
            (activeGeneration as IDisposable)?.Dispose();
            activeGeneration = null;
        }

        private static void VerifyRun(CommandState commandState)
        {
            var runDirectory = RunDirectoryPolicy.Resolve(
                commandState.outputRoot,
                commandState.runId);
            var imageCount = Directory.Exists(Path.Combine(runDirectory, "images"))
                ? Directory.GetFiles(Path.Combine(runDirectory, "images"), "frame_*.png").Length
                : 0;
            var labelCount = Directory.Exists(Path.Combine(runDirectory, "labels"))
                ? Directory.GetFiles(Path.Combine(runDirectory, "labels"), "frame_*.txt").Length
                : 0;
            var manifestPath = Path.Combine(runDirectory, "manifest.jsonl");
            var manifestCount = File.Exists(manifestPath)
                ? File.ReadLines(manifestPath).Count(line => !string.IsNullOrWhiteSpace(line))
                : 0;

            if (imageCount != commandState.frameCount ||
                labelCount != commandState.frameCount ||
                manifestCount != commandState.frameCount)
            {
                throw new InvalidDataException(
                    $"Capture produced images={imageCount}, labels={labelCount}, " +
                    $"manifest records={manifestCount}; expected {commandState.frameCount} each.");
            }

            if (!File.Exists(Path.Combine(runDirectory, "run-config.json")))
            {
                throw new InvalidDataException("Capture did not produce run-config.json.");
            }
        }

        private static void Complete(CommandState commandState)
        {
            activeState = null;
            Debug.Log(
                $"Synthetic capture run '{commandState.runId}' completed with " +
                $"{commandState.frameCount} frames.");
            EditorApplication.Exit(0);
        }

        private static void Fail(Exception exception)
        {
            activeState = null;
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }

        [Serializable]
        private sealed class CommandState
        {
            public string runId;
            public string outputRoot;
            public int frameCount;
            public int seed;

            public CommandState(CaptureRunRequest request)
            {
                runId = request.RunId;
                outputRoot = request.OutputRoot;
                frameCount = request.FrameCount;
                seed = request.Seed;
            }
        }
    }
}
