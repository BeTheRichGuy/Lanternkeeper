using System.IO;
using Lanternkeeper.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Lanternkeeper.EditorTools
{
    /// <summary>Builds <c>Scenes/Diorama.unity</c>: pixel-perfect camera with a transparent clear, and the GameBootstrap.</summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/Diorama.unity";
        const string MaterialPath = "Assets/_Game/Data/SpriteUnlit.mat";
        public const string LitMaterialPath = "Assets/_Game/Data/SpriteLit.mat";

        [MenuItem("Lanternkeeper/Build Diorama Scene")]
        public static string Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Load after NewScene: opening a scene unloads unreferenced assets and would null these out.
            var material = LoadOrCreateMaterial();
            var catalog = ArtImporter.LoadOrCreateCatalog();

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Diorama.HalfHeight;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 1f);
            cam.allowHDR = true; // the bloom needs values past 1.0 (11-22)
            cam.allowMSAA = false;
            var ppc = camGo.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = (int)IsoProjection.PPU;
            ppc.refResolutionX = Diorama.ViewWidth;
            ppc.refResolutionY = Diorama.ViewHeight;
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
            ppc.cropFrame = PixelPerfectCamera.CropFrame.None;

            var game = new GameObject("Game");
            var boot = game.AddComponent<GameBootstrap>();
            var so = new SerializedObject(boot);
            so.FindProperty("catalog").objectReferenceValue = catalog;
            so.FindProperty("spriteMaterial").objectReferenceValue = material;
            so.FindProperty("litMaterial").objectReferenceValue = LoadOrCreateMaterial(LitMaterialPath, "Universal Render Pipeline/2D/Sprite-Lit-Default");
            so.FindProperty("cam").objectReferenceValue = cam;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            return $"saved {ScenePath}; build scenes = [{ScenePath}]";
        }

        static Material LoadOrCreateMaterial() => LoadOrCreateMaterial(MaterialPath, "Universal Render Pipeline/2D/Sprite-Unlit-Default");

        public static Material LoadOrCreateMaterial(string path, string shader)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            m = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
