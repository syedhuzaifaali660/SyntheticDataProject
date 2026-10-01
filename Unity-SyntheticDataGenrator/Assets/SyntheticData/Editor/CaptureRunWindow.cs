using System.IO;
using SyntheticData.Capture;
using SyntheticData.Output;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SyntheticData.Editor
{
    public readonly struct CaptureRunRequest
    {
        public CaptureRunRequest(string runId, string outputRoot, int frameCount, int seed, bool mixedDeviceProfiles = false)
        {
            RunId = runId;
            OutputRoot = outputRoot;
            FrameCount = frameCount;
            Seed = seed;
            MixedDeviceProfiles = mixedDeviceProfiles;
        }

        public string RunId { get; }
        public string OutputRoot { get; }
        public int FrameCount { get; }
        public int Seed { get; }
        public bool MixedDeviceProfiles { get; }
    }

    public static class CaptureRunRequestValidator
    {
        public static string Validate(CaptureRunRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.OutputRoot))
            {
                return "An output root is required.";
            }

            if (request.FrameCount <= 0)
            {
                return "Frame count must be positive.";
            }

            try
            {
                RunDirectoryPolicy.Resolve(request.OutputRoot, request.RunId);
            }
            catch (System.ArgumentException exception)
            {
                return exception.Message;
            }

            return string.Empty;
        }
    }

    public static class CaptureRunPaths
    {
        public static string GetDefaultOutputRoot()
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "..",
                "Python-ModelTraining",
                "data",
                "generated"));
        }
    }

    [InitializeOnLoad]
    public sealed class CaptureRunWindow : EditorWindow
    {
        private const string PendingRequestKey = "SyntheticData.Capture.PendingRequest";
        private string runId = "smoke-20";
        private string outputRoot = CaptureRunPaths.GetDefaultOutputRoot();
        private int frameCount = 20;
        private int seed = 42;
        private bool mixedDeviceProfiles;

        static CaptureRunWindow()
        {
            EditorApplication.playModeStateChanged += StartPendingRun;
        }

        [MenuItem("Synthetic Data/Capture Run")]
        public static void Open()
        {
            GetWindow<CaptureRunWindow>("Capture Run");
        }

        private void OnGUI()
        {
            runId = EditorGUILayout.TextField("Run ID", runId);
            outputRoot = EditorGUILayout.TextField("Output Root", outputRoot);
            frameCount = EditorGUILayout.IntField("Frame Count", frameCount);
            seed = EditorGUILayout.IntField("Seed", seed);
            mixedDeviceProfiles = EditorGUILayout.Toggle("Mixed Device Profiles", mixedDeviceProfiles);

            var request = new CaptureRunRequest(runId, outputRoot, frameCount, seed, mixedDeviceProfiles);
            var error = GetValidationError(request, out _);
            if (!string.IsNullOrEmpty(error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(error)))
            {
                if (GUILayout.Button("Generate"))
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        return;
                    }

                    SessionState.SetString(PendingRequestKey, JsonUtility.ToJson(new PendingRequest(request)));
                    EditorApplication.isPlaying = true;
                }
            }
        }

        private static string GetValidationError(CaptureRunRequest request, out CaptureSceneRunner runner)
        {
            runner = null;
            var requestError = CaptureRunRequestValidator.Validate(request);
            if (!string.IsNullOrEmpty(requestError))
            {
                return requestError;
            }

            if (SceneManager.GetActiveScene().name != "PokemonCapture")
            {
                return "Open the PokemonCapture scene before generating.";
            }

            runner = Object.FindFirstObjectByType<CaptureSceneRunner>();
            if (runner == null)
            {
                return "PokemonCapture requires a CaptureSceneRunner context.";
            }

            return runner.ValidateConfiguration();
        }

        private static void StartPendingRun(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
            {
                return;
            }

            var json = SessionState.GetString(PendingRequestKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            SessionState.EraseString(PendingRequestKey);
            var pending = JsonUtility.FromJson<PendingRequest>(json);
            EditorApplication.delayCall += () =>
            {
                var request = pending.ToRequest();
                var error = GetValidationError(request, out var runner);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError("Synthetic capture run was not started: " + error);
                    return;
                }

                runner.StartRun(request.OutputRoot, request.RunId, request.FrameCount, request.Seed, request.MixedDeviceProfiles);
            };
        }

        [System.Serializable]
        private sealed class PendingRequest
        {
            public string runId;
            public string outputRoot;
            public int frameCount;
            public int seed;
            public bool mixedDeviceProfiles;

            public PendingRequest(CaptureRunRequest request)
            {
                runId = request.RunId;
                outputRoot = request.OutputRoot;
                frameCount = request.FrameCount;
                seed = request.Seed;
                mixedDeviceProfiles = request.MixedDeviceProfiles;
            }

            public CaptureRunRequest ToRequest()
            {
                return new CaptureRunRequest(runId, outputRoot, frameCount, seed, mixedDeviceProfiles);
            }
        }
    }
}
