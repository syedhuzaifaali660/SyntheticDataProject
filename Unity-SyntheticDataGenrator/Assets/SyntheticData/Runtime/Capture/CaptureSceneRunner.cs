using System;
using System.Collections;
using SyntheticData.Generation;
using SyntheticData.Labels;
using SyntheticData.Output;
using SyntheticData.Scene;
using SyntheticData.Validation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SyntheticData.Capture
{
    [DisallowMultipleComponent]
    public sealed class CaptureSceneRunner : MonoBehaviour
    {
        [SerializeField] private Camera captureCamera;
        [SerializeField] private Renderer backgroundRenderer;
        [SerializeField] private Light pointLight;
        [SerializeField] private Transform generatedObjectsContainer;
        [SerializeField] private GenerationConfig baselineConfig;
        [SerializeField] private PokemonPrefabCatalog pokemonPrefabs = new PokemonPrefabCatalog();
        [SerializeField] private BackgroundCatalog backgrounds = new BackgroundCatalog();
        [SerializeField] private BoundsOverlay boundsOverlay;

        private GenerationConfig runtimeConfig;
        private Coroutine activeRun;

        public bool IsRunning => activeRun != null;
        public GenerationConfig BaselineConfig => baselineConfig;
        public PokemonPrefabCatalog PokemonPrefabs => pokemonPrefabs;
        public BackgroundCatalog Backgrounds => backgrounds;

        public string ValidateConfiguration()
        {
            if (captureCamera == null)
            {
                return "Capture camera is required.";
            }

            if (backgroundRenderer == null)
            {
                return "Background renderer is required.";
            }

            if (pointLight == null)
            {
                return "Point light is required.";
            }

            if (generatedObjectsContainer == null)
            {
                return "Generated objects container is required.";
            }

            if (baselineConfig == null)
            {
                return "Baseline generation config is required.";
            }

            if (boundsOverlay == null)
            {
                return "Bounds overlay is required.";
            }

            try
            {
                baselineConfig.ValidateOrThrow();
                pokemonPrefabs.Validate();
                backgrounds.Validate();
            }
            catch (Exception exception)
            {
                return exception.Message;
            }

            return string.Empty;
        }

        public void StartRun(string outputRoot, string runId, int frameCount, int seed, bool mixedDeviceProfiles = false)
        {
            var generation = CreateRun(outputRoot, runId, frameCount, seed, mixedDeviceProfiles);
            activeRun = StartCoroutine(generation);
        }

        public IEnumerator CreateRun(string outputRoot, string runId, int frameCount, int seed, bool mixedDeviceProfiles = false)
        {
            if (IsRunning || runtimeConfig != null)
            {
                throw new InvalidOperationException("A capture run is already in progress.");
            }

            if (frameCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameCount), "Frame count must be positive.");
            }

            var configurationError = ValidateConfiguration();
            if (!string.IsNullOrEmpty(configurationError))
            {
                throw new InvalidOperationException(configurationError);
            }

            runtimeConfig = Instantiate(baselineConfig);
            runtimeConfig.Seed = seed;
            runtimeConfig.UseMixedDeviceProfiles = mixedDeviceProfiles;
            if (mixedDeviceProfiles)
            {
                runtimeConfig.ApplyMixedDeviceSettings();
            }
            runtimeConfig.BackgroundCount = backgrounds.Textures.Count;
            var runDirectory = RunDirectoryPolicy.Resolve(outputRoot, runId);
            return Run(runDirectory, runId, frameCount);
        }

        private IEnumerator Run(string runDirectory, string runId, int frameCount)
        {
            IEnumerator generation = null;
            try
            {
                var builder = new FrameSceneBuilder(
                    captureCamera,
                    backgroundRenderer,
                    pointLight,
                    pokemonPrefabs,
                    backgrounds,
                    (float)runtimeConfig.Width / runtimeConfig.Height,
                    generatedObjectsContainer,
                    restoreAutomaticCameraAspect: true);
                var controller = new SyntheticCaptureController(
                    captureCamera,
                    runtimeConfig,
                    new FrameRecipeSampler(runtimeConfig),
                    builder,
                    new MeshBoundsProjector(runtimeConfig.MinimumBoxPixels, runtimeConfig.MaximumCropFraction),
                    new CameraCaptureService(),
                    new AtomicCaptureWriter(runDirectory),
                    backgrounds,
                    boundsOverlay.Show,
                    boundsOverlay.Clear);
                generation = controller.GenerateRun(runId, frameCount);
                Exception failure = null;
                while (true)
                {
                    bool hasNext;
                    object current = null;
                    try
                    {
                        hasNext = generation.MoveNext();
                        if (hasNext)
                        {
                            current = generation.Current;
                        }
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                        break;
                    }

                    if (!hasNext)
                    {
                        break;
                    }

                    yield return current;
                }

                if (failure != null)
                {
                    Debug.LogException(failure, this);
                }
            }
            finally
            {
                (generation as IDisposable)?.Dispose();
                boundsOverlay.Clear();
                if (runtimeConfig != null)
                {
                    if (Application.isPlaying)
                    {
                        Object.Destroy(runtimeConfig);
                    }
                    else
                    {
                        Object.DestroyImmediate(runtimeConfig);
                    }

                    runtimeConfig = null;
                }

                activeRun = null;
            }
        }
    }
}
