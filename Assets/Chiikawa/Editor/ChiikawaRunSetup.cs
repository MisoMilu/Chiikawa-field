using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChiikawaField.Editor
{
    /// <summary>Gives the original model's disconnected limbs editable pivots and run clips.</summary>
    public static class ChiikawaRunSetup
    {
        private const string Root = "Assets/Chiikawa";
        private const string AnimationFolder = Root + "/Animations";
        private const string PartsFolder = Root + "/Models/RunParts";
        private const string RigName = "Run Rig";
        private const float Duration = 0.48f;

        private sealed class Part
        {
            public string Name;
            public Mesh Mesh;
            public Vector3 Pivot;
        }

        [MenuItem("Tools/Chiikawa/Add Running Animation")]
        public static void Setup()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Stop Play Mode before setting up the animation.");
            var player = UnityEngine.Object.FindFirstObjectByType<ChiikawaPlayerController>();
            if (player == null || player.gameObject.scene.path != "Assets/Scenes/ChiikawaField.unity")
                throw new InvalidOperationException("Open the ChiikawaField scene first.");
            var visual = player.transform.Find("Visual");
            if (visual.Find(RigName) != null)
                throw new InvalidOperationException("The running animation is already installed.");

            var model = visual.Find("Chiikawa Model");
            var filter = model.GetComponent<MeshFilter>();
            var renderer = model.GetComponent<MeshRenderer>();
            string modelPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            bool wasReadable = importer.isReadable;
            List<Part> parts;
            try
            {
                if (!wasReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }
                parts = SplitParts(filter.sharedMesh, visual.worldToLocalMatrix * model.localToWorldMatrix);
            }
            finally
            {
                if (!wasReadable)
                {
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
            }

            EnsureFolder(AnimationFolder);
            EnsureFolder(PartsFolder);
            foreach (var part in parts)
                AssetDatabase.CreateAsset(part.Mesh, PartsFolder + "/" + part.Name + ".asset");
            var controller = MakeController(parts);

            // Configure both copies without overwriting the scene's camera or movement settings.
            Configure(player, parts, renderer.sharedMaterials, controller);
            string prefabPath = Root + "/Prefabs/ChiikawaPlayer.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Configure(prefab.GetComponent<ChiikawaPlayerController>(), parts,
                    renderer.sharedMaterials, controller);
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveScene(player.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Running animation installed. Press Play and hold WASD to try it.");
        }

        private static List<Part> SplitParts(Mesh source, Matrix4x4 toVisual)
        {
            var vertices = source.vertices.Select(toVisual.MultiplyPoint3x4).ToArray();
            var triangles = source.triangles;
            var normals = source.normals;
            var uv = source.uv;
            var parent = Enumerable.Range(0, vertices.Length).ToArray();
            int Find(int i)
            {
                while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
                return i;
            }
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int root = Find(triangles[i]);
                parent[Find(triangles[i + 1])] = root;
                parent[Find(triangles[i + 2])] = root;
            }

            var bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (var v in vertices) bounds.Encapsulate(v);
            var parts = new List<Part>();
            Matrix4x4 normalMatrix = toVisual.inverse.transpose;
            foreach (var group in Enumerable.Range(0, vertices.Length).GroupBy(Find))
            {
                var indices = group.ToArray();
                var partBounds = new Bounds(vertices[indices[0]], Vector3.zero);
                foreach (int i in indices) partBounds.Encapsulate(vertices[i]);
                float height = (partBounds.center.y - bounds.min.y) / bounds.size.y;
                float width = Mathf.Abs(partBounds.center.x - bounds.center.x) / bounds.extents.x;
                string side = partBounds.center.x < bounds.center.x ? "Left" : "Right";
                string name = height < 0.15f ? side + "Foot" :
                    height > 0.88f ? side + "Ear" :
                    width > 0.55f ? side + "Arm" : height > 0.45f ? "Head" : "Body";
                bool limb = name.EndsWith("Foot") || name.EndsWith("Arm");
                Vector3 pivot = partBounds.center;
                if (limb) pivot.y = partBounds.max.y;
                var map = indices.Select((original, local) => new { original, local })
                    .ToDictionary(p => p.original, p => p.local);
                var partTriangles = new List<int>();
                for (int i = 0; i < triangles.Length; i += 3)
                    if (Find(triangles[i]) == group.Key)
                        for (int j = 0; j < 3; j++) partTriangles.Add(map[triangles[i + j]]);
                var mesh = new Mesh { name = "Chiikawa " + name };
                mesh.vertices = indices.Select(i => vertices[i] - pivot).ToArray();
                mesh.normals = indices.Select(i => normalMatrix.MultiplyVector(normals[i]).normalized).ToArray();
                mesh.uv = indices.Select(i => uv[i]).ToArray();
                mesh.triangles = partTriangles.ToArray();
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                parts.Add(new Part { Name = name, Mesh = mesh, Pivot = pivot });
            }
            string[] expected = { "Head", "Body", "LeftEar", "RightEar", "LeftArm", "RightArm", "LeftFoot", "RightFoot" };
            if (!parts.Select(p => p.Name).OrderBy(n => n).SequenceEqual(expected.OrderBy(n => n)))
                throw new InvalidOperationException("Unexpected model parts: " + string.Join(", ", parts.Select(p => p.Name)));
            return parts;
        }

        private static AnimatorController MakeController(List<Part> parts)
        {
            var idle = new AnimationClip { name = "Idle", frameRate = 60 };
            var run = new AnimationClip { name = "Run", frameRate = 60 };
            foreach (var part in parts.Where(p => p.Name.EndsWith("Foot") || p.Name.EndsWith("Arm")))
            {
                bool foot = part.Name.EndsWith("Foot");
                float side = part.Name.StartsWith("Left") ? 1f : -1f;
                SetCurve(idle, part.Name, "localEulerAnglesRaw.x", _ => 0f);
                SetCurve(run, part.Name, "localEulerAnglesRaw.x",
                    phase => Mathf.Sin(phase) * side * (foot ? 38f : -28f));
            }
            SetCurve(idle, "", "m_LocalPosition.y", _ => 0f);
            SetCurve(run, "", "m_LocalPosition.y", phase => 0.04f + 0.12f * Mathf.Pow(Mathf.Sin(phase), 2));
            SetCurve(idle, "", "localEulerAnglesRaw.x", _ => 0f);
            SetCurve(run, "", "localEulerAnglesRaw.x", _ => 6f);
            SetCurve(idle, "", "localEulerAnglesRaw.z", _ => 0f);
            SetCurve(run, "", "localEulerAnglesRaw.z", phase => Mathf.Sin(phase) * 3f);
            foreach (var clip in new[] { idle, run })
            {
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AssetDatabase.CreateAsset(clip, AnimationFolder + "/" + clip.name + ".anim");
            }
            var controller = AnimatorController.CreateAnimatorControllerAtPath(AnimationFolder + "/ChiikawaLocomotion.controller");
            controller.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);
            var machine = controller.layers[0].stateMachine;
            var idleState = machine.AddState("Idle");
            idleState.motion = idle;
            var runState = machine.AddState("Run", new Vector3(300, 0));
            runState.motion = run;
            machine.defaultState = idleState;
            var start = idleState.AddTransition(runState);
            start.hasExitTime = false;
            start.hasFixedDuration = true;
            start.duration = 0.1f;
            start.AddCondition(AnimatorConditionMode.If, 0f, "IsRunning");
            var stop = runState.AddTransition(idleState);
            stop.hasExitTime = false;
            stop.hasFixedDuration = true;
            stop.duration = 0.1f;
            stop.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsRunning");
            return controller;
        }

        private static void SetCurve(AnimationClip clip, string path, string property, Func<float, float> sample)
        {
            var curve = new AnimationCurve();
            const int samples = 32;
            for (int i = 0; i <= samples; i++)
                curve.AddKey(Duration * i / samples, sample(2f * Mathf.PI * i / samples));
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }

        private static void Configure(ChiikawaPlayerController movement, List<Part> parts,
            Material[] materials, RuntimeAnimatorController controller)
        {
            var visual = movement.transform.Find("Visual");
            var rig = new GameObject(RigName);
            rig.transform.SetParent(visual, false);
            foreach (var part in parts)
            {
                var obj = new GameObject(part.Name, typeof(MeshFilter), typeof(MeshRenderer));
                obj.transform.SetParent(rig.transform, false);
                obj.transform.localPosition = part.Pivot;
                obj.GetComponent<MeshFilter>().sharedMesh = part.Mesh;
                obj.GetComponent<MeshRenderer>().sharedMaterials = materials;
            }
            visual.Find("Chiikawa Model").gameObject.SetActive(false);
            var animator = rig.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var serialized = new SerializedObject(movement);
            serialized.FindProperty("animator").objectReferenceValue = animator;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetFileName(path));
        }
    }
}
