# Your first Chiikawa scene in Unity

## Try it

1. In Unity's Project window, open `Assets/Scenes/ChiikawaField.unity`.
2. Press the triangle **Play** button at the top of Unity.
3. Click inside the **Game** window so it receives your keyboard input.
4. Use **WASD** or the **arrow keys** to move, and **Space** to jump.
5. Press Play again to stop. Movement during Play Mode resets when you stop.

Chiikawa turns toward the direction of movement. The camera stays in one place,
like the old Godot prototype. Keep near the starting area while trying the controls.
While moving on the ground, Chiikawa swings its arms, alternates its feet, and bounces.
Releasing the movement keys blends back to idle. Jumping pauses the running animation.

## What the scene contains

- **Chiikawa Player**: the object that moves. Its Character Controller is the invisible
  collision shape that keeps Chiikawa standing on the ground.
- **Visual**, inside the player: the model that turns. The movement script rotates this
  separately from the collision shape.
- **Ground**: the pastel-green floor, with a collider to stop the player falling through.
- **Main Camera**: the player's view. It uses orthographic projection, which keeps the
  scene looking flatter rather than making distant objects look smaller.
- **Sunlight**: lighting and shadows for the model and ground.

## Where the files live

| File or folder | Purpose |
| --- | --- |
| `Assets/Scenes/ChiikawaField.unity` | The saved playable scene. |
| `Assets/Chiikawa/Models/Chiikawa.fbx` | Your 3D model, converted from the Godot GLB. |
| `Assets/Chiikawa/Textures/Chiikawa.png` | The texture containing the face artwork. Its mostly white appearance is normal: it wraps around the 3D model. |
| `Assets/Chiikawa/Materials` | Materials tell Unity how to draw the model and ground. |
| `Assets/Chiikawa/Prefabs/ChiikawaPlayer.prefab` | A reusable copy of the player setup. |
| `Assets/Chiikawa/Scripts/ChiikawaPlayerController.cs` | The C# version of the Godot movement and jump behavior. |
| `Assets/Chiikawa/Animations` | Editable Idle and Run clips and the locomotion Animator Controller. |
| `Assets/Chiikawa/Models/RunParts` | Copies of the original mesh pieces, with pivots for animating the limbs. |
| `Assets/InputSystem_Actions.inputactions` | The existing control bindings used by the movement script. |
| `Assets/Chiikawa/Model-Credits.txt` | Model author, source, license, and conversion credit. |

To change movement speed, stop Play Mode, select **Chiikawa Player** in the Hierarchy,
and find **Chiikawa Player Controller** in the Inspector. Start with **Speed**. The defaults
ported from Godot are speed 4, turn speed 10, jump velocity 5, and gravity 14.715.
Save the scene after changing its Inspector settings.

## What was transferred

The model and face texture were reused. The ground size and color came from the Godot
scene. The camera was framed for Unity, and movement uses the camera's horizontal
forward/right directions so W goes toward the top of the view. The movement behavior
was rewritten in C#: Unity does not execute Godot's `.gd` scripts or `.tscn` scenes.

The original Godot folder was only read. `SampleScene` remains in Assets; the new field
is the first enabled scene in Unity's build scene list.

The source model has no skeleton or animation clips. Its separate mesh pieces now form
a **Run Rig** inside **Visual**, with editable animations made in Unity. The original
**Chiikawa Model** is kept inactive as a reference. The movement script sets the
Animator's **IsRunning** parameter when Chiikawa moves on the ground; the Animator
blends between **Idle** and **Run**. Root motion is disabled so movement and collisions
remain controlled by the Character Controller. Grass picking, the painted start menu,
and the ramen scene are future features.

To adjust the motion, open `Assets/Chiikawa/Animations/Run.anim` in Unity's Animation
window. The clip controls the arm and foot swings, body lean, and bounce. The
`ChiikawaLocomotion.controller` asset contains the Idle/Run transitions.

## Verification

The new scripts compiled with no errors. A Play Mode check using a temporary virtual
keyboard verified grounded movement, turning, jumping, landing, and normalized diagonal
movement. Input settings were restored after testing, and Play Mode was stopped.

The running animation also passed a virtual-keyboard Play Mode check covering each
WASD direction, alternating feet, body bounce, turning, release-to-idle with pose reset,
diagonal speed, jumping, and landing. The animated scene and reusable prefab are saved.

## Conversion and setup tools

You do not need these to play or edit the scene:

- `Tools/Chiikawa/convert_model.py` converts the original GLB to FBX using Blender.
- `Assets/Chiikawa/Editor/ChiikawaSceneSetup.cs` created the scene and materials once.
  Its menu command refuses to overwrite an existing ChiikawaField scene.
- `Assets/Chiikawa/Editor/ChiikawaRunSetup.cs` adds the mesh pivots, clips, and Animator
  to the field scene and reusable player prefab. Its menu command refuses to run twice.
- `Tools/Chiikawa/VerifyMovement.cs` is the optional Play Mode verification script.
- `Tools/Chiikawa/VerifyRunning.cs` checks the run animation and movement together.
