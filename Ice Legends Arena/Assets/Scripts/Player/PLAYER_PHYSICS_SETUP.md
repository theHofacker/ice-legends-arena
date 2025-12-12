# Player Movement Physics Setup Guide

This guide walks you through setting up the player with ice hockey physics in Unity Editor.

## Overview
You'll be creating:
1. IcePhysics physics material for low friction
2. Player with Rigidbody2D and physics-based movement
3. Rink boundaries to constrain player movement
4. Player prefab for reusability

---

## Step 1: Create Ice Physics Material

1. **Navigate to Physics folder**:
   - In Project window, go to `Assets/Physics/`

2. **Create Physics Material 2D**:
   - Right-click in Physics folder → Create → 2D → Physics Material 2D
   - Name it "IcePhysics"

3. **Configure IcePhysics**:
   - Select the IcePhysics material
   - In Inspector, set:
     - **Friction**: 0.1 (low friction for ice sliding)
     - **Bounciness**: 0 (no bouncing)

---

## Step 2: Update TestPlayer to Player with Physics

### 2.1: Remove Old Components

1. **Select TestPlayer** in Hierarchy

2. **Remove BoxCollider** (3D collider):
   - In Inspector, find "Box Collider" component
   - Click ⋮ menu (three dots) → Remove Component

3. **Disable TestPlayerMovement** (keep for reference):
   - Find "Test Player Movement (Script)" component
   - Uncheck the box next to the component name to disable it

### 2.2: Add Rigidbody2D

1. **Add Rigidbody2D component**:
   - Click "Add Component"
   - Search for "Rigidbody2D"
   - Add it

2. **Configure Rigidbody2D**:
   - **Body Type**: Dynamic
   - **Material**: None (will set on collider)
   - **Mass**: 1
   - **Linear Drag**: 0
   - **Angular Drag**: 5
   - **Gravity Scale**: 0 (IMPORTANT for top-down)
   - **Collision Detection**: Continuous
   - **Sleeping Mode**: Start Awake
   - **Interpolate**: Interpolate
   - **Constraints**: Freeze Rotation Z (check the box)

### 2.3: Add CircleCollider2D

1. **Add CircleCollider2D**:
   - Click "Add Component"
   - Search for "Circle Collider 2D"
   - Add it

2. **Configure CircleCollider2D**:
   - **Material**: IcePhysics (drag from Physics folder or click circle icon to select)
   - **Is Trigger**: Unchecked
   - **Radius**: 0.5
   - **Offset**: X: 0, Y: 0

### 2.4: Add PlayerController Script

1. **Add PlayerController**:
   - Click "Add Component"
   - Search for "Player Controller"
   - Add it

2. **Verify Settings** (should be defaults):
   - **Max Speed**: 5
   - **Acceleration**: 10
   - **Deceleration**: 15
   - **Show Velocity Gizmo**: Checked

### 2.5: Rename GameObject

1. **Rename to "Player"**:
   - Select TestPlayer in Hierarchy
   - Press F2 or right-click → Rename
   - Change name to "Player"

---

## Step 3: Create Rink Boundaries

### 3.1: Create Parent GameObject

1. **Create empty GameObject**:
   - Right-click in Hierarchy → Create Empty
   - Name it "RinkBoundary"
   - Set Position: (0, 0, 0)

### 3.2: Create Wall GameObjects

**For each wall (North, South, East, West), follow these steps:**

#### Wall North (Top)

1. **Create child**:
   - Right-click RinkBoundary → Create Empty
   - Name: "WallNorth"

2. **Add BoxCollider2D**:
   - Select WallNorth
   - Add Component → Box Collider 2D

3. **Configure**:
   - **Position**: X: 0, Y: 15, Z: 0
   - **Box Collider 2D**:
     - Size X: 60
     - Size Y: 1
     - Is Trigger: Unchecked

#### Wall South (Bottom)

1. Create child: "WallSouth"
2. Add Component → Box Collider 2D
3. Configure:
   - **Position**: X: 0, Y: -15, Z: 0
   - **Size**: X: 60, Y: 1

#### Wall East (Right)

1. Create child: "WallEast"
2. Add Component → Box Collider 2D
3. Configure:
   - **Position**: X: 30, Y: 0, Z: 0
   - **Size**: X: 1, Y: 30

#### Wall West (Left)

1. Create child: "WallWest"
2. Add Component → Box Collider 2D
3. Configure:
   - **Position**: X: -30, Y: 0, Z: 0
   - **Size**: X: 1, Y: 30

### 3.3: Optional - Visual Indicators

If you want to see the boundaries in the Game view (helpful for debugging):

1. **Add Sprite Renderer to each wall**:
   - Select a wall → Add Component → Sprite Renderer
   - Sprite: UI-Sprite (built-in white square)
   - Color: Red with alpha 0.2 (semi-transparent)
   - Sorting Layer: Default
   - Order in Layer: -1 (behind player)

---

## Step 4: Create Player Prefab

1. **Drag Player to Prefabs folder**:
   - Select "Player" in Hierarchy
   - Drag it to `Assets/Prefabs/Player/` folder
   - This creates "Player.prefab"

2. **Verify prefab connection**:
   - Player name in Hierarchy should turn blue
   - This means it's linked to the prefab

3. **Test prefab**:
   - Delete Player from scene
   - Drag Player.prefab back to scene
   - Should work identically

---

## Step 5: Save Scene and Test

1. **Save Scene**: File → Save (Ctrl+S)

2. **Enter Play Mode**: Click Play button

3. **Test Movement**:
   - **Keyboard**: Use WASD or Arrow keys
   - **Joystick**: Click and drag virtual joystick

4. **Observe**:
   - ✅ Player should accelerate smoothly
   - ✅ Player should slide to a stop (ice feel)
   - ✅ Player should stop at rink boundaries
   - ✅ No spinning or jittering
   - ✅ Green velocity vector visible in Scene view (if gizmos enabled)

---

## Troubleshooting

### Problem: Player doesn't move

**Solutions**:
- Check InputManager exists in scene and is enabled
- Check PlayerController has reference to InputManager
- Check Rigidbody2D is **Dynamic** (not Kinematic)
- Check "Gravity Scale" is 0

### Problem: Player spins when moving

**Solutions**:
- Check Rigidbody2D Constraints → Freeze Rotation Z is checked
- Increase Angular Drag to 10

### Problem: Player too slippery / slides too far

**Solutions**:
- Increase **Deceleration** value (try 20-25)
- Increase IcePhysics **Friction** (try 0.15-0.2)

### Problem: Player not slippery enough / stops too fast

**Solutions**:
- Decrease **Deceleration** value (try 8-10)
- Decrease IcePhysics **Friction** (try 0.05)

### Problem: Player passes through walls

**Solutions**:
- Set Rigidbody2D Collision Detection to **Continuous**
- Make walls thicker (Size Y: 2 instead of 1)
- Check walls have BoxCollider2D with "Is Trigger" unchecked

### Problem: Movement is jittery

**Solutions**:
- Check Rigidbody2D Interpolate is set to **Interpolate**
- Ensure physics updates in FixedUpdate (PlayerController already does this)

### Problem: No console errors but player still doesn't move

**Diagnostics**:
1. Select Player in Hierarchy while in Play Mode
2. Check PlayerController component shows:
   - Max Speed, Acceleration, Deceleration values
3. Open Console (Ctrl+Shift+C)
4. Look for error: "InputManager instance not found!"
5. If error appears, check InputManager GameObject exists and is enabled

---

## Tuning Parameters

### Speed Feel Too Slow?
- Increase **Max Speed** (try 7-8)
- Increase **Acceleration** (try 15-20)

### Speed Feel Too Fast?
- Decrease **Max Speed** (try 3-4)
- Decrease **Acceleration** (try 5-8)

### Want More Ice Slide?
- Decrease **Deceleration** (try 8-10)
- Decrease IcePhysics Friction (try 0.05)

### Want More Control (Less Slide)?
- Increase **Deceleration** (try 20-25)
- Increase IcePhysics Friction (try 0.15-0.2)

---

## Next Steps After Setup

1. **Adjust Camera**:
   - Position camera to view the rink from above
   - Consider using Cinemachine for smooth following (future)

2. **Add Visuals**:
   - Replace cube with player sprite or 3D model (future)
   - Add animation controller (future)
   - Add particle effects for ice spray (future)

3. **Test Thoroughly**:
   - Test all 8 directions (N, NE, E, SE, S, SW, W, NW)
   - Test diagonal vs cardinal speed (should be same)
   - Test bouncing off walls
   - Test with both keyboard and joystick

4. **Optional Enhancements**:
   - Add sprint mechanic
   - Add directional facing (rotate to face movement)
   - Add collision with other players (future multiplayer)

---

## Summary Checklist

Before moving on, ensure:

- [x] **IcePhysics material created** (Friction 0.1, Bounciness 0)
- [x] **Player has Rigidbody2D** (Gravity Scale 0, Freeze Rotation Z)
- [x] **Player has CircleCollider2D** (Radius 0.5, Material: IcePhysics)
- [x] **Player has PlayerController** script
- [x] **TestPlayerMovement disabled** (not removed, just unchecked)
- [x] **BoxCollider (3D) removed** from player
- [x] **RinkBoundary created** with 4 walls
- [x] **Each wall has BoxCollider2D** with correct position and size
- [x] **Player prefab created** in Prefabs/Player/
- [x] **Movement tested** and working smoothly
- [x] **Ice physics feel** achieved (slides, not instant stop)
- [x] **Boundaries working** (can't leave rink)

**If all boxes are checked, Issue #3 is complete!**
