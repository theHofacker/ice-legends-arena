# Virtual Joystick Setup Guide

This guide walks you through setting up the Virtual Joystick in Unity Editor.

## Overview
You'll be creating:
1. Canvas with proper scaling
2. Virtual Joystick UI hierarchy
3. InputManager GameObject
4. EventSystem configuration

---

## Step 1: Create Canvas

1. **Open SampleScene** (`Assets/Scenes/SampleScene.unity`)

2. **Create Canvas**:
   - Right-click in Hierarchy → UI → Canvas
   - This will auto-create an EventSystem (we'll configure it later)

3. **Configure Canvas**:
   - Select the Canvas GameObject
   - In Inspector, set:
     - **Render Mode**: Screen Space - Overlay
     - **Pixel Perfect**: ☑ (checked)

4. **Configure Canvas Scaler**:
   - Select the Canvas GameObject
   - Find Canvas Scaler component
   - Set:
     - **UI Scale Mode**: Scale With Screen Size
     - **Reference Resolution**: X: 1920, Y: 1080
     - **Screen Match Mode**: Match Width Or Height
     - **Match**: 0.5 (middle position)

---

## Step 2: Create Virtual Joystick Hierarchy

1. **Create Joystick Parent**:
   - Right-click Canvas → Create Empty
   - Rename to "VirtualJoystick"

2. **Configure VirtualJoystick RectTransform**:
   - Select VirtualJoystick GameObject
   - In RectTransform component:
     - Click anchor preset (top-left square icon)
     - Hold ALT+SHIFT and click **bottom-left** preset (anchors AND pivots to bottom-left)
     - Set **Pos X**: 150
     - Set **Pos Y**: 150
     - Set **Width**: 200
     - Set **Height**: 200

3. **Create Background Image**:
   - Right-click VirtualJoystick → UI → Image
   - Rename to "Background"
   - Configure:
     - **Anchors**: Center (0.5, 0.5)
     - **Pos X**: 0
     - **Pos Y**: 0
     - **Width**: 150
     - **Height**: 150
     - **Source Image**: Knob (Unity's built-in circle sprite)
       - If Knob doesn't appear, select "UI-Sprite" from dropdown
     - **Color**: White with alpha 0.3 (R:255, G:255, B:255, A:77)
     - **Raycast Target**: ☑ (checked) - IMPORTANT!

4. **Create Handle Image**:
   - Right-click VirtualJoystick → UI → Image
   - Rename to "Handle"
   - Configure:
     - **Anchors**: Center (0.5, 0.5)
     - **Pos X**: 0
     - **Pos Y**: 0
     - **Width**: 60
     - **Height**: 60
     - **Source Image**: Knob (same as background)
     - **Color**: White with alpha 0.6 (R:255, G:255, B:255, A:153)
     - **Raycast Target**: ☐ (unchecked) - Only background needs raycasts!

5. **Add VirtualJoystick Script**:
   - Select the VirtualJoystick GameObject
   - Click "Add Component"
   - Search for and add "Virtual Joystick" script
   - Configure:
     - **Background**: Drag the Background GameObject here
     - **Handle**: Drag the Handle GameObject here
     - **Handle Range**: 50
     - **Dead Zone**: 0.1
     - **Return To Center**: ☑ (checked)
     - **Return Speed**: 10
     - **Active Alpha**: 1
     - **Inactive Alpha**: 0.5

---

## Step 3: Configure EventSystem

1. **Select EventSystem** GameObject in Hierarchy

2. **Remove Standalone Input Module**:
   - Find "Standalone Input Module" component
   - Click the ⋮ menu (three dots) → Remove Component

3. **Add Input System UI Input Module**:
   - Click "Add Component"
   - Search for "Input System UI Input Module"
   - Add it

4. **Configure Input System UI Input Module**:
   - **Actions Asset**: Drag `Assets/InputSystem_Actions.inputactions` here
   - It will automatically configure the UI action map bindings

---

## Step 4: Create InputManager GameObject

1. **Create Empty GameObject**:
   - Right-click in Hierarchy → Create Empty
   - Rename to "InputManager"
   - Position: (0, 0, 0) - doesn't matter for logic objects

2. **Add InputManager Script**:
   - Select InputManager GameObject
   - Click "Add Component"
   - Search for and add "Input Manager" script

3. **Configure InputManager**:
   - **Virtual Joystick**: Drag the VirtualJoystick GameObject from Hierarchy here
   - **Input Actions Asset**: Drag `Assets/InputSystem_Actions.inputactions` here

---

## Step 5: Save and Test

1. **Save Scene**: Ctrl+S (Cmd+S on Mac)

2. **Enter Play Mode**: Click Play button

3. **Test with Mouse**:
   - Click and drag on the joystick in the Game view
   - The handle should move with your mouse
   - When you release, it should return to center
   - The alpha should change when you touch/release

4. **Verify in Inspector** (while in Play Mode):
   - Select InputManager GameObject
   - Watch the "Move Input" value change as you drag the joystick
   - Select VirtualJoystick GameObject
   - Watch the "Input Vector" value in the inspector

---

## Step 6: Create Prefab (Optional but Recommended)

1. **Create Prefab Folder** (if not exists):
   - In Project window, navigate to `Assets/Prefabs/`
   - Create subfolder called "UI"

2. **Create Prefab**:
   - Drag the entire **VirtualJoystick** GameObject from Hierarchy
   - Drop it into `Assets/Prefabs/UI/` folder
   - Name it "MobileJoystick"

3. **Benefits**:
   - Reusable in other scenes
   - Easy to update all instances
   - Can instantiate at runtime if needed

---

## Troubleshooting

### Joystick doesn't respond to clicks:
- ✅ Background Image has "Raycast Target" checked
- ✅ EventSystem exists in scene
- ✅ Canvas has a GraphicRaycaster component (should be added automatically)
- ✅ VirtualJoystick script has Background and Handle references assigned

### Handle doesn't move:
- ✅ VirtualJoystick script references are assigned correctly
- ✅ Handle Range is > 0 (default: 50)
- ✅ Check console for errors

### InputManager shows errors:
- ✅ InputSystem_Actions asset is assigned
- ✅ VirtualJoystick reference is assigned
- ✅ Check that InputSystem_Actions.cs is generated (should happen automatically)

### Joystick appears but Input Vector stays at zero:
- ✅ Dead Zone isn't too high (should be 0.1)
- ✅ Handle Range is reasonable (50 is good default)
- ✅ Try dragging further from center

### Alpha/visual feedback not working:
- ✅ Background and Handle both have Image components
- ✅ Active/Inactive Alpha values are different
- ✅ Images are using colors with alpha channel

---

## Next Steps: Testing with PlayerController

To actually see movement, you'll need to create a simple test player:

```csharp
using UnityEngine;

public class TestPlayer : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private void Update()
    {
        if (InputManager.Instance != null)
        {
            Vector2 moveInput = InputManager.Instance.MoveInput;
            transform.position += new Vector3(moveInput.x, 0, moveInput.y) * moveSpeed * Time.deltaTime;
        }
    }
}
```

1. Create a Cube in the scene
2. Attach this script to it
3. Enter Play Mode
4. Use the joystick to move the cube around!

---

## Unity Remote Testing (Optional)

To test on actual mobile device:

1. **Install Unity Remote app** on iOS/Android device
2. **Connect device** via USB
3. **In Unity**:
   - Edit → Project Settings → Editor
   - Device: Select your device
4. **Enter Play Mode** in Unity
5. **Touch joystick on device screen** - it should work!

Note: Unity Remote mirrors the game view to your device, so you can test touch input without building.

---

## Build Settings for Mobile Testing

When ready to build:

1. **File → Build Settings**
2. **Add Open Scenes** (add SampleScene)
3. **Platform**: Switch to Android or iOS
4. **Player Settings**:
   - Company Name, Product Name
   - Minimum API Level (Android) / Target SDK (iOS)
5. **Build and Run**

The virtual joystick should automatically activate on mobile!

---

## Summary

You should now have:
- ✅ Canvas with proper scaling
- ✅ VirtualJoystick UI (Background + Handle)
- ✅ VirtualJoystick.cs script attached and configured
- ✅ EventSystem with Input System UI Input Module
- ✅ InputManager GameObject with script attached
- ✅ All references properly assigned

**The joystick should work with mouse in editor, and will work with touch on mobile!**
