using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SyntheticData.Labels
{
    public sealed class MeshVertexCache
    {
        private static readonly ConditionalWeakTable<GameObject, MeshVertexCache> sharedCaches =
            new ConditionalWeakTable<GameObject, MeshVertexCache>();
        public readonly struct Entry
        {
            public Transform Transform { get; }
            public Vector3[] LocalVertices { get; }
            public bool VerticesIncludeTransformScale { get; }

            public Entry(
                Transform transform,
                Vector3[] localVertices,
                bool verticesIncludeTransformScale = false)
            {
                Transform = transform;
                LocalVertices = localVertices;
                VerticesIncludeTransformScale = verticesIncludeTransformScale;
            }
        }

        private readonly List<Entry> entries;

        private MeshVertexCache(List<Entry> entries)
        {
            this.entries = entries;
        }

        public IReadOnlyList<Entry> Entries => entries;

        public static MeshVertexCache GetOrCreate(GameObject target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            return sharedCaches.GetValue(target, Capture);
        }

        public static MeshVertexCache Capture(GameObject target)
        {
            var capturedEntries = new List<Entry>();
            foreach (var meshFilter in target.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = meshFilter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                if (!mesh.isReadable)
                {
                    throw new InvalidOperationException(
                        $"Cannot project unreadable mesh '{mesh.name}' on '{meshFilter.name}'. " +
                        "Enable GLTFAST_KEEP_MESH_DATA and reimport the glTF asset.");
                }

                var localVertices = mesh.vertices;
                if (localVertices.Length > 0)
                {
                    capturedEntries.Add(new Entry(meshFilter.transform, localVertices));
                }
            }

            foreach (var skinnedRenderer in target.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bakedMesh = new Mesh();
                try
                {
                    skinnedRenderer.BakeMesh(bakedMesh);
                    if (!bakedMesh.isReadable)
                    {
                        throw new InvalidOperationException(
                            $"Cannot project unreadable baked mesh on '{skinnedRenderer.name}'.");
                    }

                    var localVertices = bakedMesh.vertices;
                    if (localVertices.Length > 0)
                    {
                        // BakeMesh already includes the renderer hierarchy's scale (including
                        // the generated root's recipe scale). Applying TransformPoint would
                        // scale these vertices a second time for glTF skinned meshes.
                        capturedEntries.Add(new Entry(
                            skinnedRenderer.transform,
                            localVertices,
                            verticesIncludeTransformScale: true));
                    }
                }
                finally
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(bakedMesh);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(bakedMesh);
                    }
                }
            }

            return new MeshVertexCache(capturedEntries);
        }
    }
}
