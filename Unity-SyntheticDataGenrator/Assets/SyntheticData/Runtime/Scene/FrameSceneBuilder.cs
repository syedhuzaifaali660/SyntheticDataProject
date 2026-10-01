using System;
using System.Collections.Generic;
using SyntheticData.Domain;
using SyntheticData.Generation;
using SyntheticData.Labels;
using UnityEngine;
using UnityEngine.Rendering;

namespace SyntheticData.Scene
{
    public sealed class FrameSceneBuilder
    {
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseMapScaleAndOffset = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int Brightness = Shader.PropertyToID("_Brightness");
        private static readonly int Contrast = Shader.PropertyToID("_Contrast");
        private static readonly int Blur = Shader.PropertyToID("_Blur");
        private static readonly int Temperature = Shader.PropertyToID("_Temperature");

        private readonly Camera camera;
        private readonly Renderer backgroundRenderer;
        private readonly Light pointLight;
        private readonly PokemonPrefabCatalog pokemonPrefabs;
        private readonly BackgroundCatalog backgrounds;
        private readonly float outputAspect;
        private readonly Transform generatedObjectsContainer;
        private readonly bool restoreAutomaticCameraAspect;

        public FrameSceneBuilder(
            Camera camera,
            Renderer backgroundRenderer,
            Light pointLight,
            PokemonPrefabCatalog pokemonPrefabs,
            BackgroundCatalog backgrounds,
            float outputAspect,
            Transform generatedObjectsContainer = null,
            bool restoreAutomaticCameraAspect = true)
        {
            this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
            this.backgroundRenderer = backgroundRenderer ?? throw new ArgumentNullException(nameof(backgroundRenderer));
            this.pointLight = pointLight ?? throw new ArgumentNullException(nameof(pointLight));
            this.pokemonPrefabs = pokemonPrefabs ?? throw new ArgumentNullException(nameof(pokemonPrefabs));
            this.backgrounds = backgrounds ?? throw new ArgumentNullException(nameof(backgrounds));
            this.generatedObjectsContainer = generatedObjectsContainer;
            this.restoreAutomaticCameraAspect = restoreAutomaticCameraAspect;
            if (float.IsNaN(outputAspect) || float.IsInfinity(outputAspect) || outputAspect <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(outputAspect),
                    "Output aspect must be finite and positive.");
            }

            this.outputAspect = outputAspect;
        }

        public SpawnedFrame Build(FrameRecipe recipe)
        {
            if (recipe == null)
            {
                throw new ArgumentNullException(nameof(recipe));
            }

            pokemonPrefabs.Validate();
            backgrounds.Validate();
            var state = SceneState.Capture(
                camera,
                backgroundRenderer,
                pointLight,
                restoreAutomaticCameraAspect);
            GameObject generatedObjects = null;
            try
            {
                var recipeAspect = recipe.OutputWidth > 0 && recipe.OutputHeight > 0
                    ? recipe.OutputWidth / (float)recipe.OutputHeight
                    : outputAspect;
                ApplySceneState(recipe, recipeAspect);
                generatedObjects = new GameObject(
                    generatedObjectsContainer == null ? "GeneratedObjects" : "GeneratedFrame");
                if (generatedObjectsContainer != null)
                {
                    generatedObjects.transform.SetParent(generatedObjectsContainer, false);
                }
                var spawnedObjects = new List<SpawnedObject>();
                foreach (var objectRecipe in recipe.Objects)
                {
                    var pokemonClass = ToPokemonClass(objectRecipe.ClassId);
                    var root = UnityEngine.Object.Instantiate(
                        pokemonPrefabs.Get(pokemonClass),
                        generatedObjects.transform);
                    root.transform.position = GetWorldPosition(objectRecipe, recipeAspect);
                    root.transform.rotation = Quaternion.Euler(objectRecipe.EulerAngles);
                    root.transform.localScale = Vector3.one * objectRecipe.Scale;
                    if (recipe.CaptureProfile != null && recipe.CaptureProfile.Name != "Baseline")
                    {
                        ProjectedObjectPlacement.Fit(camera, root, recipe, objectRecipe);
                    }
                    var label = root.GetComponent<PokemonLabel>();
                    var vertexCache = MeshVertexCache.GetOrCreate(root);
                    spawnedObjects.Add(new SpawnedObject(root, label, vertexCache));
                }

                return new SpawnedFrame(generatedObjects.transform, spawnedObjects, state);
            }
            catch
            {
                Destroy(generatedObjects);
                state.Restore();
                throw;
            }
        }

        public void Clear(SpawnedFrame frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            frame.Clear();
        }

        private void ApplySceneState(FrameRecipe recipe, float recipeAspect)
        {
            camera.fieldOfView = recipe.CameraFov;
            camera.aspect = recipeAspect;
            var backgroundUvScale = 1f / recipe.Background.Scale;
            var maximumBackgroundOffset = 1f - backgroundUvScale;
            var centeredBackgroundOffset = maximumBackgroundOffset * 0.5f;
            var backgroundOffset = new Vector2(
                Mathf.Clamp(
                    centeredBackgroundOffset + recipe.Background.CropOffset.x,
                    0f,
                    maximumBackgroundOffset),
                Mathf.Clamp(
                    centeredBackgroundOffset + recipe.Background.CropOffset.y,
                    0f,
                    maximumBackgroundOffset));
            var properties = new MaterialPropertyBlock();
            properties.SetTexture(BaseMap, backgrounds.Get(recipe.Background.Index));
            properties.SetVector(
                BaseMapScaleAndOffset,
                new Vector4(
                    backgroundUvScale,
                    backgroundUvScale,
                    backgroundOffset.x,
                    backgroundOffset.y));
            if (recipe.CaptureProfile != null && recipe.CaptureProfile.Name != "Baseline")
            {
                // Cover the entire native viewport, preserving the source background's aspect ratio.
                var texture = backgrounds.Get(recipe.Background.Index);
                var sourceAspect = texture.width / (float)texture.height;
                var uvScale = new Vector2(Mathf.Min(1, recipeAspect / sourceAspect), Mathf.Min(1, sourceAspect / recipeAspect)) / recipe.Background.Scale;
                var uvOffset = new Vector2(
                    Mathf.Clamp((1 - uvScale.x) * .5f + recipe.Background.CropOffset.x, 0, 1 - uvScale.x),
                    Mathf.Clamp((1 - uvScale.y) * .5f + recipe.Background.CropOffset.y, 0, 1 - uvScale.y));
                properties.SetVector(BaseMapScaleAndOffset, new Vector4(uvScale.x, uvScale.y, uvOffset.x, uvOffset.y));
                var depth = 20f;
                foreach (var item in recipe.Objects) depth = Mathf.Max(depth, item.Distance + 20f);
                depth = Mathf.Min(depth, camera.farClipPlane * .9f);
                var mesh = backgroundRenderer.GetComponent<MeshFilter>().sharedMesh;
                var parentScale = backgroundRenderer.transform.parent == null ? Vector3.one : backgroundRenderer.transform.parent.lossyScale;
                var height = 2 * depth * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f) * 1.001f;
                backgroundRenderer.transform.SetPositionAndRotation(camera.transform.position + camera.transform.forward * depth, camera.transform.rotation);
                backgroundRenderer.transform.localScale = new Vector3(height * recipeAspect / mesh.bounds.size.x / parentScale.x, height / mesh.bounds.size.y / parentScale.y, 1);
            }
            properties.SetFloat(Brightness, recipe.Background.Brightness);
            properties.SetFloat(Contrast, recipe.Background.Contrast);
            properties.SetFloat(Blur, recipe.Background.Blur);
            properties.SetFloat(Temperature, recipe.Background.Temperature);
            backgroundRenderer.SetPropertyBlock(properties);
            pointLight.transform.position = recipe.Light.PointPosition;
            pointLight.intensity = recipe.Light.PointIntensity;
            pointLight.colorTemperature = recipe.Light.Temperature;
            pointLight.useColorTemperature = true;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white;
            RenderSettings.ambientIntensity = recipe.Light.AmbientIntensity;
        }

        private Vector3 GetWorldPosition(ObjectRecipe recipe, float recipeAspect)
        {
            var halfHeight = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var cameraDirection = new Vector3(
                (recipe.Center.x * 2f - 1f) * halfHeight * recipeAspect,
                (recipe.Center.y * 2f - 1f) * halfHeight,
                1f).normalized;
            var worldDirection = camera.transform.TransformDirection(cameraDirection);
            return camera.transform.position + worldDirection * recipe.Distance;
        }

        private static PokemonClass ToPokemonClass(int classId)
        {
            if (!Enum.IsDefined(typeof(PokemonClass), classId))
            {
                throw new InvalidOperationException($"Recipe contains invalid Pokemon class ID {classId}.");
            }

            return (PokemonClass)classId;
        }

        private static void Destroy(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
