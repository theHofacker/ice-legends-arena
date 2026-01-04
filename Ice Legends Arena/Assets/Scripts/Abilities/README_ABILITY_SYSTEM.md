# Ability System Framework - Setup Guide

Complete ability system with cooldowns, activation, and UI integration!

## What's Implemented ✅

**Ability.cs** - Abstract base class for all abilities
- Cooldown system (configurable per ability)
- CanUseAbility() check
- TryActivateAbility() with events
- Abstract ActivateAbility() for custom logic
- Progress tracking (0-100%)

**MeteorStrike.cs** - Example ability implementation
- Elementalist's Meteor Strike ability
- Spawns meteor at target location
- Stuns nearby opponents
- Demonstrates inheritance pattern

**AbilityButton.cs** - UI button component
- Shows cooldown progress (radial fill)
- Displays remaining time
- Disables button during cooldown
- Auto-updates with ability changes

---

## How the System Works

### 1. **Inheritance Pattern**
```
Ability (abstract base)
  ├── MeteorStrike (Elementalist)
  ├── TemporalRewind (Chronomancer)
  ├── PhantomStep (Shadow Assassin)
  └── ... (other abilities)
```

### 2. **Cooldown Flow**
```
1. Player clicks Ability button
2. AbilityButton calls ability.TryActivateAbility()
3. Ability checks CanUseAbility() (cooldown check)
4. If ready: ActivateAbility() → custom logic executes
5. StartCooldown() → timer starts (e.g., 35 seconds)
6. Update() counts down cooldown each frame
7. When cooldown reaches 0 → ability ready again
```

### 3. **Events System**
- `OnAbilityActivated` - Fired when ability is used
- `OnCooldownChanged` - Fired every frame during cooldown
- `OnAbilityEnded` - Fired when ability effect ends (duration-based)

---

## Unity Setup (Step-by-Step)

### Step 1: Add Ability Component to Player

1. **Select Player** (or Player (1), Player (2), etc.) in Hierarchy
2. **In Inspector**, click **Add Component**
3. Search for **"Meteor Strike"** (or the ability you want)
4. **Add it**

**Configure Ability:**
- The ability will use the AbilityData ScriptableObject you already created
- Explosion Radius: `5`
- Stun Duration: `2`
- Damage: `25`

---

### Step 2: Create Ability Button in UI

1. **In Hierarchy**, right-click **GameHUD** → **UI** → **Button - TextMeshPro**
2. **Rename it:** `AbilityButton`
3. **In Inspector (RectTransform)**:
   - Anchor Preset: **Bottom Right**
   - Pos X: **-150**, Pos Y: **150**
   - Width: **120**, Height: **120**

**Add Cooldown Overlay:**
1. **Right-click AbilityButton** → **UI** → **Image**
2. **Rename it:** `CooldownOverlay`
3. **In Inspector (RectTransform)**:
   - Anchor Preset: **Stretch** (fill parent)
   - Left/Right/Top/Bottom: **0**
4. **In Inspector (Image)**:
   - Color: **Black**, Alpha: **0.7**
   - Image Type: **Filled**
   - Fill Method: **Radial 360**
   - Fill Origin: **Top**
   - Fill Amount: **1** (will be controlled by script)

**Add Cooldown Text:**
1. **Right-click AbilityButton** → **UI** → **Text - TextMeshPro**
2. **Rename it:** `CooldownText`
3. **In Inspector (RectTransform)**:
   - Anchor Preset: **Middle Center**
   - Width: **100**, Height: **50**
4. **In Inspector (TextMeshPro)**:
   - Text: `12.5s`
   - Font Size: **32**
   - Alignment: **Center & Middle**
   - Color: **White**
   - Font Style: **Bold**

---

### Step 3: Add AbilityButton Component

1. **Select AbilityButton** in Hierarchy
2. **In Inspector**, the Button component should already exist
3. **Add Component** → Search for **"Ability Button"**
4. **Drag references**:
   - Cooldown Overlay → Drag `CooldownOverlay` image
   - Cooldown Text → Drag `CooldownText`
   - Ability Icon → Drag `Image` component of button itself
5. **Configure colors**:
   - Ready Color: **White**
   - Cooldown Color: **Gray** (RGB: 128, 128, 128)

---

### Step 4: Connect Button to Ability

1. **In Inspector (Button component)** of AbilityButton:
   - Click **+** under **OnClick()**
   - Drag **AbilityButton** itself into the object field
   - Function: **AbilityButton** → **OnButtonClick()**

2. **In Play Mode**, the button will automatically find the player's ability
   - If using PlayerManager (character switching), it will update when you switch characters

---

### Step 5: Link Ability to AbilityButton (Manual for MVP)

For MVP, manually link the ability to the button:

1. **Create a simple script** to connect them on Start:
```csharp
// AbilityButtonSetup.cs - attach to AbilityButton
using UnityEngine;

public class AbilityButtonSetup : MonoBehaviour
{
    private void Start()
    {
        // Find player's ability
        MeteorStrike ability = FindObjectOfType<MeteorStrike>();

        // Get button component
        AbilityButton button = GetComponent<AbilityButton>();

        // Connect them
        if (ability != null && button != null)
        {
            button.SetAbility(ability);
        }
    }
}
```

2. **Attach this script to AbilityButton**

---

## Testing the System

**Press Play** ▶️

You should see:
```
✅ Ability button appears (bottom-right)
✅ Button shows ability name (e.g., "METEOR STRIKE")
✅ Button is clickable (not grayed out)
```

**Click the Ability button:**
```
✅ Console: "⚡ Meteor Strike ACTIVATED!"
✅ Console: "Meteor Strike activated at (position)!"
✅ Console: "Meteor Strike on cooldown for 35.0s"
✅ Cooldown overlay fills the button
✅ Cooldown text shows "35.0s" → counts down
✅ Button is grayed out (not clickable)
```

**Wait for cooldown:**
```
✅ Text counts down: 34.9s, 34.8s, ...
✅ Overlay empties gradually (radial fill)
✅ At 0.0s: overlay disappears, button becomes clickable again
```

---

## Creating Your Own Abilities

### Example: Temporal Rewind (Chronomancer)

```csharp
using UnityEngine;

public class TemporalRewind : Ability
{
    [Header("Temporal Rewind Settings")]
    [SerializeField] private float rewindDuration = 2f; // Rewind 2 seconds back

    // Store puck position history
    private Queue<Vector2> puckPositionHistory = new Queue<Vector2>();

    protected override void ActivateAbility()
    {
        // Rewind puck to position from 2 seconds ago
        GameObject puck = GameObject.FindGameObjectWithTag("Puck");
        if (puck != null && puckPositionHistory.Count > 0)
        {
            Vector2 rewindPosition = puckPositionHistory.Dequeue();
            puck.transform.position = rewindPosition;

            Debug.Log($"Puck rewound to {rewindPosition}!");
        }
    }

    // Store position history in Update
    private void LateUpdate()
    {
        GameObject puck = GameObject.FindGameObjectWithTag("Puck");
        if (puck != null)
        {
            puckPositionHistory.Enqueue(puck.transform.position);

            // Keep only last 2 seconds of history (120 frames at 60fps)
            if (puckPositionHistory.Count > 120)
                puckPositionHistory.Dequeue();
        }
    }
}
```

### Steps to Create New Ability:

1. **Create new C# script** (e.g., `PhantomStep.cs`)
2. **Inherit from Ability**: `public class PhantomStep : Ability`
3. **Override ActivateAbility()**: Implement your ability logic
4. **Add component to Player**
5. **Link to AbilityButton**

---

## Advanced Features

### Custom Cooldown Conditions

Override `CanUseAbility()` to add custom conditions:

```csharp
public override bool CanUseAbility()
{
    // Check base cooldown first
    if (!base.CanUseAbility())
        return false;

    // Add custom conditions
    bool hasEnoughMana = playerMana >= abilityCost;
    bool isGrounded = playerController.IsGrounded();

    return hasEnoughMana && isGrounded;
}
```

### Ability Meter System

Add meter filling logic (e.g., fills with goals scored, hits landed, etc.):

```csharp
private float abilityMeter = 0f;

public void OnGoalScored()
{
    abilityMeter += 25f; // 4 goals = 100% meter
    if (abilityMeter >= 100f)
    {
        // Unlock ability
        Debug.Log("Ability meter full!");
    }
}

public override bool CanUseAbility()
{
    return base.CanUseAbility() && abilityMeter >= 100f;
}
```

---

## Character Portrait Integration (Later)

When you create character portraits with ability meters:

```csharp
// In CharacterPortrait.cs
private void SetupAbilityButton()
{
    AbilityButton button = GetComponentInChildren<AbilityButton>();
    Ability ability = character.GetComponent<Ability>();

    if (button != null && ability != null)
    {
        button.SetAbility(ability);
    }
}
```

---

## 3D Transfer Ready ✅

**100% transferable to 2.5D/3D!**
- Ability.cs = pure logic (no 2D dependencies)
- MeteorStrike.cs = uses Vector2/Vector3 interchangeably
- AbilityButton.cs = UI only (works in any dimension)
- Just change Vector2 → Vector3 in ability scripts!

---

**Your Ability System is ready!** Click that button and watch the meteors fall! ☄️⚡
