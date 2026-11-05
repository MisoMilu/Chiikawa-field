using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class VerifyRunning
{
    public static async Task<object> Run()
    {
        if (!Application.isPlaying) throw new Exception("Enter Play Mode first.");
        var movement = UnityEngine.Object.FindFirstObjectByType<ChiikawaField.ChiikawaPlayerController>();
        var player = movement.transform;
        var controller = movement.GetComponent<CharacterController>();
        var visual = player.Find("Visual");
        var rig = visual.Find("Run Rig");
        var animator = rig.GetComponent<Animator>();
        var left = rig.Find("LeftFoot");
        var right = rig.Find("RightFoot");
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var actions = (InputActionAsset)movement.GetType().GetField("runtimeActions", flags).GetValue(movement);
        float speed = (float)movement.GetType().GetField("speed", flags).GetValue(movement);
        var oldDevices = actions.devices;
        var keyboard = InputSystem.AddDevice<Keyboard>("ChiikawaRunVerification");
        var settings = InputSystem.settings;
        var oldBackground = settings.backgroundBehavior;
        var oldFocus = settings.editorInputBehaviorInPlayMode;
        bool oldRunInBackground = Application.runInBackground;
        var checks = new List<string>();
        int firstFrame = Time.frameCount;
        try
        {
            Application.runInBackground = true;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            actions.devices = new InputDevice[] { keyboard };
            InputSystem.EnableDevice(keyboard);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            await Wait(0.3f);
            Require(controller.isGrounded, "Initially grounded", checks);
            Require(!animator.GetBool("IsRunning") && animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "Idle without input", checks);

            Vector3 origin = player.position;
            float maxFootSwing = 0f, minBounce = float.MaxValue, maxBounce = float.MinValue;
            foreach (var key in new[] { Key.W, Key.A, Key.S, Key.D })
            {
                ResetPosition(controller, origin);
                Vector3 start = player.position;
                float startTime = Time.time;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                await Wait(0.22f);
                Require(animator.GetBool("IsRunning") && animator.GetCurrentAnimatorStateInfo(0).IsName("Run"), key + " starts Run", checks);
                for (int i = 0; i < 5; i++)
                {
                    await Wait(0.035f);
                    float l = Mathf.DeltaAngle(0f, left.localEulerAngles.x);
                    float r = Mathf.DeltaAngle(0f, right.localEulerAngles.x);
                    maxFootSwing = Mathf.Max(maxFootSwing, Mathf.Abs(l));
                    Require(Mathf.Abs(l + r) < 0.5f, key + " feet alternate " + i, checks);
                    minBounce = Mathf.Min(minBounce, rig.localPosition.y);
                    maxBounce = Mathf.Max(maxBounce, rig.localPosition.y);
                }
                float elapsed = Time.time - startTime;
                Vector3 delta = player.position - start;
                delta.y = 0f;
                Require(Mathf.Abs(delta.magnitude / elapsed - speed) < speed * 0.15f, key + " keeps movement speed", checks);
                Vector3 expected = key == Key.W ? Vector3.forward : key == Key.S ? Vector3.back : key == Key.A ? Vector3.left : Vector3.right;
                Require(Vector3.Dot(visual.forward, expected) > 0.95f, key + " faces movement", checks);
                if (key == Key.S) Capture("/private/tmp/chiikawa-running.png");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                await Wait(0.22f);
                Require(!animator.GetBool("IsRunning") && animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), key + " release returns Idle", checks);
                Require(Quaternion.Angle(left.localRotation, Quaternion.identity) < 0.1f &&
                    Quaternion.Angle(right.localRotation, Quaternion.identity) < 0.1f && rig.localPosition.sqrMagnitude < 0.0001f,
                    key + " release restores pose", checks);
            }
            Require(maxFootSwing > 30f, "Visible foot swing", checks);
            Require(maxBounce - minBounce > 0.07f, "Body bounce advances", checks);

            ResetPosition(controller, origin);
            float diagonalStart = Time.time;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D));
            await Wait(0.3f);
            Vector3 diagonal = player.position - origin;
            diagonal.y = 0f;
            Require(Mathf.Abs(diagonal.magnitude / (Time.time - diagonalStart) - speed) < speed * 0.15f,
                "Diagonal movement stays normalized", checks);
            float floor = player.position.y;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D, Key.Space));
            await Wait(0.12f);
            Require(player.position.y > floor + 0.2f && !controller.isGrounded && !animator.GetBool("IsRunning"),
                "Jump rises and pauses Run", checks);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            await Wait(1.4f);
            Require(controller.isGrounded && Mathf.Abs(player.position.y - floor) < 0.12f, "Jump lands", checks);
            ResetPosition(controller, origin);
            visual.rotation = Quaternion.Euler(0f, 180f, 0f);
            await Wait(0.2f);
            Capture("/private/tmp/chiikawa-idle.png");
            return new { passed = true, checks = checks.Count, framesAdvanced = Time.frameCount - firstFrame,
                maxFootSwing, bounceRange = maxBounce - minBounce, speed,
                runningCapture = "/private/tmp/chiikawa-running.png", idleCapture = "/private/tmp/chiikawa-idle.png" };
        }
        finally
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            actions.devices = oldDevices;
            InputSystem.RemoveDevice(keyboard);
            settings.backgroundBehavior = oldBackground;
            settings.editorInputBehaviorInPlayMode = oldFocus;
            Application.runInBackground = oldRunInBackground;
        }
    }

    private static void Require(bool condition, string message, List<string> checks)
    {
        if (!condition) throw new Exception("Failed: " + message);
        checks.Add(message);
    }

    private static void ResetPosition(CharacterController controller, Vector3 position)
    {
        controller.enabled = false;
        controller.transform.position = position;
        controller.enabled = true;
    }

    private static async Task Wait(float seconds)
    {
        float end = Time.time + seconds;
        float deadline = Time.realtimeSinceStartup + seconds + 10f;
        while (Time.time < end)
        {
            if (Time.realtimeSinceStartup > deadline) throw new Exception("Play Mode did not advance.");
            EditorApplication.QueuePlayerLoopUpdate();
            await Task.Delay(10);
        }
    }

    private static void Capture(string path)
    {
        var camera = Camera.main;
        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        var target = RenderTexture.GetTemporary(960, 720, 24);
        var texture = new Texture2D(960, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 960, 720), 0, 0);
            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.Destroy(texture);
        }
    }
}
