# HUD & In-Game UI - Setup Guide

Complete in-game HUD with score, timer, pause button, and goal celebrations!

## What's Implemented ✅

**HUDManager.cs** - Main HUD controller
- Score display (Player vs Opponent)
- Match timer countdown
- Goal celebration overlay
- Pause menu integration
- Subscribes to GameManager events for real-time updates

---

## Unity Setup (Step-by-Step)

### Step 1: Create UI Canvas

1. **In Hierarchy**, right-click → **UI** → **Canvas**
2. **Rename it:** `GameHUD`
3. **In Inspector**, configure Canvas:
   - Render Mode: **Screen Space - Overlay**
   - Canvas Scaler:
     - UI Scale Mode: **Scale With Screen Size**
     - Reference Resolution: **1920 x 1080**
     - Match: **0.5** (balance between width and height)

---

### Step 2: Create Score Display (Top Center)

1. **Right-click GameHUD** → **UI** → **Text - TextMeshPro**
   - If prompted to import TMP Essentials, click **Import**
2. **Rename it:** `ScoreText`
3. **In Inspector (RectTransform)**:
   - Anchor Preset: **Top Center**
   - Pos Y: **-50** (50 pixels from top)
   - Width: **200**, Height: **60**
4. **In Inspector (TextMeshPro)**:
   - Text: `0 - 0`
   - Font Size: **48**
   - Alignment: **Center & Middle**
   - Color: **White**
   - Font Style: **Bold**

---

### Step 3: Create Timer Display (Top Center, below score)

1. **Right-click GameHUD** → **UI** → **Text - TextMeshPro**
2. **Rename it:** `TimerText`
3. **In Inspector (RectTransform)**:
   - Anchor Preset: **Top Center**
   - Pos Y: **-120** (below score)
   - Width: **150**, Height: **50**
4. **In Inspector (TextMeshPro)**:
   - Text: `5:00`
   - Font Size: **42**
   - Alignment: **Center & Middle**
   - Color: **White**

---

### Step 4: Create Goal Celebration Text (Center Screen)

1. **Right-click GameHUD** → **UI** → **Text - TextMeshPro**
2. **Rename it:** `GoalCelebrationText`
3. **In Inspector (RectTransform)**:
   - Anchor Preset: **Middle Center** (hold Alt and click)
   - Width: **600**, Height: **150**
4. **In Inspector (TextMeshPro)**:
   - Text: `GOAL!`
   - Font Size: **100**
   - Alignment: **Center & Middle**
   - Color: **Green**
   - Font Style: **Bold**
   - Outline: Enable, Width: **0.2**, Color: **Black**

---

### Step 5: Create Pause Button (Top-Right)

1. **Right-click GameHUD** → **UI** → **Button - TextMeshPro**
2. **Rename it:** `PauseButton`
3. **In Inspector (RectTransform)**:
   - Anchor Preset: **Top Right**
   - Pos X: **-100**, Pos Y: **-50**
   - Width: **120**, Height: **60**
4. **Select child** `Text (TMP)`:
   - Text: `PAUSE`
   - Font Size: **32**
   - Color: **White**

---

### Step 6: Create Pause Menu Panel

1. **Right-click GameHUD** → **UI** → **Panel**
2. **Rename it:** `PauseMenuPanel`
3. **In Inspector (Image component)**:
   - Anchor Preset: **Stretch** (fill screen)
   - Color: **Black**, Alpha: **0.8** (semi-transparent background)

**Add Resume Button:**
1. **Right-click PauseMenuPanel** → **UI** → **Button - TextMeshPro**
2. **Rename it:** `ResumeButton`
3. **In Inspector (RectTransform)**:
   - Anchor Preset: **Middle Center**
   - Pos Y: **50** (above center)
   - Width: **250**, Height: **80**
4. **Select child** `Text (TMP)`:
   - Text: `RESUME`
   - Font Size: **42**
   - Color: **White**
5. **In Inspector (Button component)**:
   - Click **+** under **OnClick()**
   - Drag **GameHUD** into the object field
   - Function: **HUDManager** → **ResumeMatch()**

**Add Restart Button:**
1. **Right-click PauseMenuPanel** → **UI** → **Button - TextMeshPro**
2. **Rename it:** `RestartButton`
3. **In Inspector (RectTransform)**:
   - Anchor Preset: **Middle Center**
   - Pos Y: **-50** (below center)
   - Width: **250**, Height: **80**
4. **Select child** `Text (TMP)`:
   - Text: `RESTART`
   - Font Size: **42**
   - Color: **White**
5. **In Inspector (Button component)**:
   - Click **+** under **OnClick()**
   - Drag **GameHUD** into the object field
   - Function: **HUDManager** → **RestartMatch()**

**Add "PAUSED" Title Text:**
1. **Right-click PauseMenuPanel** → **UI** → **Text - TextMeshPro**
2. **Rename it:** `PausedTitleText`
3. **In Inspector (RectTransform)**:
   - Anchor Preset: **Top Center**
   - Pos Y: **-150**
   - Width: **400**, Height: **100**
4. **In Inspector (TextMeshPro)**:
   - Text: `PAUSED`
   - Font Size: **72**
   - Alignment: **Center & Middle**
   - Color: **White**
   - Font Style: **Bold**

---

### Step 7: Add HUDManager Component

1. **Select GameHUD** in Hierarchy
2. **In Inspector**, click **Add Component**
3. Search for **"HUD Manager"** and add it
4. **Drag references**:
   - Score Text → Drag `ScoreText` into the field
   - Timer Text → Drag `TimerText` into the field
   - Goal Celebration Text → Drag `GoalCelebrationText` into the field
   - Pause Menu Panel → Drag `PauseMenuPanel` into the field
5. **Configure settings**:
   - Goal Celebration Duration: `2`
   - Show Debug Messages: ✓ (checked)

---

### Step 8: Connect Pause Button

1. **Select PauseButton** in Hierarchy
2. **In Inspector** (Button component):
   - Click **+** under **OnClick()**
   - Drag **GameHUD** into the object field
   - Function: **HUDManager** → **TogglePause()**

---

## Testing the HUD

**Press Play** ▶️

You should see:
```
✅ Score: 0 - 0 (top center)
✅ Timer: 5:00 (below score)
✅ Pause button (top-right)
```

**Score a goal:**
```
✅ "GOAL!" appears in green (center screen)
✅ Score updates (e.g., 1 - 0)
✅ Disappears after 2 seconds
```

**Watch timer:**
```
✅ Counts down: 4:59, 4:58, 4:57...
✅ Updates every second
```

**Click Pause:**
```
✅ Pause menu appears (black overlay)
✅ Game freezes (Time.timeScale = 0)
✅ "PAUSED" title shows
✅ Resume button works
✅ Restart button works
```

**Note:** ESC key support would require adding a Pause action to the InputSystem_Actions asset (uses new Unity Input System, not legacy Input class).

---

## Customization

### Change Colors:
- **Score/Timer**: Select text, change Color in TextMeshPro
- **Goal Celebration**: Change color based on who scored (green = player, red = opponent)

### Change Font Size:
- **Score**: 48 (current) → adjust for your preference
- **Timer**: 42 (current) → adjust for your preference

### Change Positions:
- Use **Pos X/Y** in RectTransform
- Use **Anchor Presets** to align to screen edges

---

## Next Steps (Optional Enhancements)

### Character Portraits (Issue #48 - Part 2):
- Add 5 character portrait images (top-right)
- Show ability cooldown meters
- Highlight active character

### Enhanced Goal Celebration:
- Add particle effects
- Add screen shake
- Add sound effects

### Pause Menu Features:
- Resume button
- Restart button
- Settings button
- Quit button

---

## 3D Transfer Ready ✅

**100% transferable to 2.5D/3D!**
- HUDManager = pure logic (no 2D dependencies)
- All UI is screen-space overlay
- Just copy the Canvas to your 3D scene!

---

**Your HUD is ready!** Press Play and you'll see the score and timer! 🏒📊
