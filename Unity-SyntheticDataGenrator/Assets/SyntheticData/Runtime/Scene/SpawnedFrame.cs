using System;
using System.Collections.Generic;
using SyntheticData.Domain;
using SyntheticData.Labels;
using UnityEngine;
using UnityEngine.Rendering;

namespace SyntheticData.Scene
{
    public readonly struct SpawnedObject
    {
        public GameObject Root { get; }
        public PokemonLabel Label { get; }
        public MeshVertexCache VertexCache { get; }

        public SpawnedObject(GameObject root, PokemonLabel label, MeshVertexCache vertexCache)
        {
            Root = root;
            Label = label;
            VertexCache = vertexCache;
        }
    }

    public sealed class SpawnedFrame
    {
        private readonly SceneState sceneState;
        private readonly IReadOnlyList<GameObject> roots;

        internal SpawnedFrame(
            Transform generatedObjectsRoot,
            IReadOnlyList<SpawnedObject> spawnedObjects,
            SceneState sceneState)
        {
            GeneratedObjectsRoot = generatedObjectsRoot;
            SpawnedObjects = spawnedObjects;
            this.sceneState = sceneState;
            var registeredRoots = new GameObject[spawnedObjects.Count];
            for (var index = 0; index < spawnedObjects.Count; index++)
            {
                registeredRoots[index] = spawnedObjects[index].Root;
            }

            roots = registeredRoots;
        }

        public Transform GeneratedObjectsRoot { get; }
        public IReadOnlyList<SpawnedObject> SpawnedObjects { get; }
        public IReadOnlyList<GameObject> Roots => roots;
        public bool IsCleared { get; private set; }

        public MeshVertexCache GetCache(GameObject root)
        {
            foreach (var spawnedObject in SpawnedObjects)
            {
                if (spawnedObject.Root == root)
                {
                    return spawnedObject.VertexCache;
                }
            }

            throw new InvalidOperationException("The object is not registered in this spawned frame.");
        }

        internal void Clear()
        {
            if (IsCleared)
            {
                return;
            }

            sceneState.Restore();
            foreach (var root in roots)
            {
                Destroy(root);
            }

            Destroy(GeneratedObjectsRoot == null ? null : GeneratedObjectsRoot.gameObject);
            IsCleared = true;
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

    internal sealed class SceneState
    {
        private readonly Camera camera;
        private readonly float cameraFov;
        private readonly float cameraAspect;
        private readonly bool restoreAutomaticCameraAspect;
        private readonly Renderer backgroundRenderer;
        private readonly MaterialPropertyBlock backgroundProperties;
        private readonly Vector3 backgroundPosition;
        private readonly Quaternion backgroundRotation;
        private readonly Vector3 backgroundScale;
        private readonly Light pointLight;
        private readonly Vector3 lightLocalPosition;
        private readonly Quaternion lightLocalRotation;
        private readonly Vector3 lightLocalScale;
        private readonly float lightIntensity;
        private readonly float lightTemperature;
        private readonly bool lightUsesTemperature;
        private readonly float ambientIntensity;
        private readonly AmbientMode ambientMode;
        private readonly Color ambientLight;

        private SceneState(
            Camera camera,
            Renderer backgroundRenderer,
            Light pointLight,
            bool restoreAutomaticCameraAspect)
        {
            this.camera = camera;
            cameraFov = camera.fieldOfView;
            cameraAspect = camera.aspect;
            this.restoreAutomaticCameraAspect = restoreAutomaticCameraAspect;
            this.backgroundRenderer = backgroundRenderer;
            backgroundProperties = new MaterialPropertyBlock();
            backgroundRenderer.GetPropertyBlock(backgroundProperties);
            backgroundPosition = backgroundRenderer.transform.position;
            backgroundRotation = backgroundRenderer.transform.rotation;
            backgroundScale = backgroundRenderer.transform.localScale;
            this.pointLight = pointLight;
            lightLocalPosition = pointLight.transform.localPosition;
            lightLocalRotation = pointLight.transform.localRotation;
            lightLocalScale = pointLight.transform.localScale;
            lightIntensity = pointLight.intensity;
            lightTemperature = pointLight.colorTemperature;
            lightUsesTemperature = pointLight.useColorTemperature;
            ambientIntensity = RenderSettings.ambientIntensity;
            ambientMode = RenderSettings.ambientMode;
            ambientLight = RenderSettings.ambientLight;
        }

        public static SceneState Capture(
            Camera camera,
            Renderer backgroundRenderer,
            Light pointLight,
            bool restoreAutomaticCameraAspect)
        {
            return new SceneState(
                camera,
                backgroundRenderer,
                pointLight,
                restoreAutomaticCameraAspect);
        }

        public void Restore()
        {
            if (camera != null)
            {
                camera.fieldOfView = cameraFov;
                if (restoreAutomaticCameraAspect)
                {
                    camera.ResetAspect();
                }
                else
                {
                    camera.aspect = cameraAspect;
                }
            }

            if (backgroundRenderer != null)
            {
                backgroundRenderer.SetPropertyBlock(backgroundProperties);
                backgroundRenderer.transform.SetPositionAndRotation(backgroundPosition, backgroundRotation);
                backgroundRenderer.transform.localScale = backgroundScale;
            }

            if (pointLight != null)
            {
                pointLight.transform.localPosition = lightLocalPosition;
                pointLight.transform.localRotation = lightLocalRotation;
                pointLight.transform.localScale = lightLocalScale;
                pointLight.intensity = lightIntensity;
                pointLight.colorTemperature = lightTemperature;
                pointLight.useColorTemperature = lightUsesTemperature;
            }

            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientLight = ambientLight;
        }
    }
}
