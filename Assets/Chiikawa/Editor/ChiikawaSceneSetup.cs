using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace ChiikawaField.Editor
{
    /// <summary>Builds the initial Godot prototype as normal, editable Unity assets.</summary>
    public static class ChiikawaSceneSetup
    {
        private const string Root = "Assets/Chiikawa";
        private const string ScenePath = "Assets/Scenes/ChiikawaField.unity";

        [MenuItem("Tools/Chiikawa/Create Initial Field Scene")]
        public static void CreateScene()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Stop Play Mode before creating the scene.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("ChiikawaField already exists. Open it from Assets/Scenes instead.");
            if (Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
                .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save your open scene before creating the field.");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/Chiikawa.fbx");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Chiikawa.png");
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            if (model == null || texture == null || actions == null)
                throw new InvalidOperationException("Import the model, texture, and input actions first.");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.85f, 0.88f, 0.9f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;

            var bodyMaterial = MakeMaterial("Chiikawa", "Universal Render Pipeline/Lit", Color.white);
            bodyMaterial.SetTexture("_BaseMap", texture);
            bodyMaterial.SetFloat("_Smoothness", 0f);
            bodyMaterial.SetFloat("_Metallic", 0f);
            bodyMaterial.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(bodyMaterial);
            var grassMaterial = MakeMaterial("Pastel Ground", "Universal Render Pipeline/Lit",
                new Color(185f / 255f, 230f / 255f, 162f / 255f));
            grassMaterial.SetFloat("_Smoothness", 0f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -0.12f, 0f);
            ground.transform.localScale = new Vector3(47.72f, 0.24f, 47.72f);
            ground.GetComponent<Renderer>().sharedMaterial = grassMaterial;

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7.5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 150f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.8f, 0.9f, 0.96f);
            cameraObject.transform.position = new Vector3(0f, 10f, -12f);
            cameraObject.transform.LookAt(new Vector3(0f, 1.5f, 0f));

            var lightObject = new GameObject("Sunlight", typeof(Light));
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.91f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            var player = new GameObject("Chiikawa Player");
            var controller = player.AddComponent<CharacterController>();
            var visual = new GameObject("Visual").transform;
            visual.SetParent(player.transform, false);
            var modelObject = (GameObject)PrefabUtility.InstantiatePrefab(model);
            modelObject.name = "Chiikawa Model";
            modelObject.transform.SetParent(visual, false);
            foreach (var renderer in modelObject.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = Enumerable.Repeat(bodyMaterial, renderer.sharedMaterials.Length).ToArray();

            // Normalize source units; the feet are at Y=0 and the turn pivot is centered.
            Bounds bounds = GetBounds(modelObject);
            modelObject.transform.localScale *= 3.8f / bounds.size.y;
            bounds = GetBounds(modelObject);
            modelObject.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            bounds = GetBounds(modelObject);
            controller.height = 3.8f;
            controller.radius = Mathf.Min(1.2f, Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.8f);
            controller.center = new Vector3(0f, 1.9f, 0f);
            controller.skinWidth = 0.04f;
            controller.stepOffset = 0.25f;
            controller.minMoveDistance = 0f;
            player.transform.position = new Vector3(0f, 0.05f, 0f);
            visual.rotation = Quaternion.Euler(0f, 180f, 0f);

            var movement = player.AddComponent<ChiikawaPlayerController>();
            var serialized = new SerializedObject(movement);
            serialized.FindProperty("visual").objectReferenceValue = visual;
            serialized.FindProperty("viewCamera").objectReferenceValue = cameraObject.transform;
            serialized.FindProperty("inputActions").objectReferenceValue = actions;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(player, Root + "/Prefabs/ChiikawaPlayer.prefab");

            EditorSceneManager.SaveScene(scene, ScenePath);
            var otherScenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath)
                .Select(s => new EditorBuildSettingsScene(s.path, false));
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(otherScenes).ToArray();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0, 1.9f, 0), cameraObject.transform.rotation, 9f, true);
            Debug.Log("Chiikawa Field created. Press Play, click the Game view, then use WASD and Space.");
        }

        private static Material MakeMaterial(string name, string shaderName, Color color)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Missing shader: " + shaderName);
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, Root + "/Materials/" + name + ".mat");
            return material;
        }

        private static Bounds GetBounds(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("Model contains no visible mesh.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
