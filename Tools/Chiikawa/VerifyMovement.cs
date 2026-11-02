using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class VerifyMovement
{
    public static async Task<object> Run()
    {
        if (!Application.isPlaying) throw new Exception("Enter Play Mode first.");
        var player = GameObject.Find("Chiikawa Player");
        var controller = player.GetComponent<CharacterController>();
        var visual = player.transform.Find("Visual");
        var keyboard = InputSystem.AddDevice<Keyboard>("ChiikawaVerificationKeyboard");
        var movement = player.GetComponent<ChiikawaField.ChiikawaPlayerController>();
        var field = typeof(ChiikawaField.ChiikawaPlayerController).GetField("runtimeActions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var actions = (InputActionAsset)field.GetValue(movement);
        actions.devices = new InputDevice[] { keyboard };
        var settings = InputSystem.settings;
        var oldBehavior = settings.backgroundBehavior;
        var oldFocus = settings.editorInputBehaviorInPlayMode;
        settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.EnableDevice(keyboard);
        try
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            await Wait(0.5f);
            var start = player.transform.position;
            bool initiallyGrounded = controller.isGrounded;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            await Wait(0.5f);
            var observed = actions.FindAction("Player/Move").ReadValue<Vector2>();
            bool keyDown = keyboard.dKey.isPressed;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            await Wait(0.1f);
            float distance = player.transform.position.x - start.x;
            float facing = Vector3.Dot(visual.forward, Vector3.right);
            float floor = player.transform.position.y;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            await Wait(0.15f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            float jumpRise = player.transform.position.y - floor;
            await Wait(1f);
            bool landed = controller.isGrounded && Mathf.Abs(player.transform.position.y - floor) < 0.12f;
            var beforeDiagonal = player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D));
            await Wait(0.5f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            await Wait(0.05f);
            var diagonal = player.transform.position - beforeDiagonal;
            diagonal.y = 0f;
            bool passed = initiallyGrounded && distance > 1.5f && distance < 2.6f && facing > 0.95f && jumpRise > 0.2f && landed && diagonal.magnitude > 1.5f && diagonal.magnitude < 2.6f;
            return new { observed = observed.ToString(), keyDown, deviceEnabled = keyboard.enabled, controlCount = actions.FindAction("Player/Move").controls.Count, passed, initiallyGrounded, rightwardDistance = distance, rightFacingDot = facing, jumpRise, landed, diagonalDistance = diagonal.magnitude };
        }
        finally
        {
            actions.devices = null;
            InputSystem.RemoveDevice(keyboard);
            settings.backgroundBehavior = oldBehavior;
            settings.editorInputBehaviorInPlayMode = oldFocus;
        }
    }

    private static async Task Wait(float seconds)
    {
        float end = Time.time + seconds;
        float deadline = Time.realtimeSinceStartup + seconds + 10f;
        while (Time.time < end)
        {
            if (Time.realtimeSinceStartup > deadline) throw new Exception("Play Mode did not advance.");
            await Task.Delay(10);
        }
    }
}
