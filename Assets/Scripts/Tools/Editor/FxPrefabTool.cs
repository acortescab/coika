using System.IO;
using Coika.Data;
using Coika.Fx;
using Coika.UI;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Coika.Tools
{
    /// <summary>
    /// Creates or updates the particle assets of issue #32 and makes them Addressable in the Fx group (C-01): the
    /// point-sampled pixel and ring textures, two unlit materials and the ParticleSpawner prefab with its two
    /// particle systems, then points the <see cref="GameSceneInstaller"/> of the Game scene at the prefab. Idempotent.
    /// </summary>
    public static class FxPrefabTool
    {
        public const string PrefabPath = "Assets/Prefabs/Fx/ParticleSpawner.prefab";
        public const string PixelTexturePath = "Assets/Art/Sprites/Fx/FxPixel.png";
        public const string RingTexturePath = "Assets/Art/Sprites/Fx/FxRing.png";
        public const string FxGroupName = "FX";

        /// <summary>Sorting order of the particles: above the pieces (2) and the guide line (3), below the Danger Line (10).</summary>
        public const int SORTING_ORDER = 5;

        private const string PixelMaterialPath = "Assets/Prefabs/Fx/FxPixel.mat";
        private const string RingMaterialPath = "Assets/Prefabs/Fx/FxRing.mat";
        private const string ScenePath = "Assets/Scenes/GameScene.unity";
        private const int RING_TEXTURE_SIZE = 64;
        private const int RING_THICKNESS = 3;

        /// <summary>
        /// Builds the textures, materials and prefab, registers them in the Fx group and wires the Game scene.
        /// </summary>
        [MenuItem("Coika/Setup Particles")]
        public static void Run()
        {
            var pixel = CreateTexture(PixelTexturePath, 2, (_, _) => true);
            var ring = CreateTexture(RingTexturePath, RING_TEXTURE_SIZE, IsOnRing);
            if (pixel == null || ring == null)
            {
                Debug.LogError("Setup Particles stopped: a texture was not created.");
                return;
            }

            var pixelMaterial = CreateMaterial(PixelMaterialPath, pixel);
            var ringMaterial = CreateMaterial(RingMaterialPath, ring);
            BuildPrefab(pixelMaterial, ringMaterial);
            MakeAddressable(PixelTexturePath);
            MakeAddressable(RingTexturePath);
            MakeAddressable(PrefabPath);
            WireScene();
            Debug.Log($"Particles are up to date at {PrefabPath}.");
        }

        /// <summary>
        /// Tells whether a pixel of the ring texture is part of the ring.
        /// </summary>
        /// <param name="x">Pixel column.</param>
        /// <param name="y">Pixel row.</param>
        /// <returns>True within <see cref="RING_THICKNESS"/> pixels of the edge of the circle.</returns>
        private static bool IsOnRing(int x, int y)
        {
            var half = RING_TEXTURE_SIZE * 0.5f;
            var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half));
            return distance <= half && distance > half - RING_THICKNESS;
        }

        /// <summary>
        /// Writes a white texture whose pixels are on where the predicate says so, imported point-sampled, without
        /// compression or mipmaps (GDD §10).
        /// </summary>
        private static Texture2D CreateTexture(string path, int size, System.Func<int, int, bool> isOn)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, isOn(x, y) ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// Creates or updates an unlit sprite material with the texture. It uses the same shader as the pieces, so it
        /// supports the particle colour and alpha.
        /// </summary>
        private static Material CreateMaterial(string path, Texture2D texture)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.mainTexture = texture;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Builds the prefab: the spawner on the root and one child particle system for the pixels and one for the rings.
        /// </summary>
        private static void BuildPrefab(Material pixelMaterial, Material ringMaterial)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var root = new GameObject("ParticleSpawner");
            try
            {
                var spawner = root.AddComponent<ParticleSpawner>();
                var particles = CreateSystem(root.transform, "Particles", pixelMaterial, false);
                var rings = CreateSystem(root.transform, "Rings", ringMaterial, true);

                var serialized = new SerializedObject(spawner);
                serialized.FindProperty("_particles").objectReferenceValue = particles;
                serialized.FindProperty("_rings").objectReferenceValue = rings;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Adds a world-space particle system that emits nothing by itself: the spawner emits every particle. Particles
        /// fade out; rings also grow from a fifth of their size.
        /// </summary>
        private static ParticleSystem CreateSystem(Transform parent, string name, Material material, bool isRing)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var system = child.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = isRing ? new ParticleSystem.MinMaxCurve(0f) : new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.gravityModifier = isRing ? 0f : 0.4f;
            main.maxParticles = isRing ? 8 : 160;

            var emission = system.emission;
            emission.enabled = false;

            var shape = system.shape;
            shape.enabled = !isRing;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.05f;

            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(fade);

            if (isRing)
            {
                var size = system.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0.2f, 0f, 4f),
                    new Keyframe(1f, 1f, 0.2f, 0f)));
            }

            var renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = SORTING_ORDER;
            return system;
        }

        /// <summary>
        /// Registers an asset in the Fx Addressables group. Logs an error when the group is missing.
        /// </summary>
        /// <param name="path">Asset path.</param>
        private static void MakeAddressable(string path)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var group = settings != null ? settings.FindGroup(FxGroupName) : null;
            if (group == null)
            {
                Debug.LogError($"Addressables group '{FxGroupName}' not found; {path} was not made Addressable.");
                return;
            }

            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group, false, false);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Opens the Game scene, points the installer at the prefab and saves the scene.
        /// </summary>
        private static void WireScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameSceneInstaller installer = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                installer = root.GetComponentInChildren<GameSceneInstaller>(true);
                if (installer != null)
                {
                    break;
                }
            }

            if (installer == null)
            {
                Debug.LogError($"Setup Particles stopped: no GameSceneInstaller in {ScenePath}.");
                return;
            }

            var serialized = new SerializedObject(installer);
            serialized.FindProperty("_fxPrefab.m_AssetGUID").stringValue = AssetDatabase.AssetPathToGUID(PrefabPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
