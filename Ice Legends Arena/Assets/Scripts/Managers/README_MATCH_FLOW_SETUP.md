# Match Flow & Game Loop - Setup Guide

Complete hockey match experience with timer, scoring, face-offs, and win/lose conditions!

## What's Implemented ✅

**GameManager.cs** - Match controller singleton
- 5:00 match timer countdown
- Score tracking (Player vs Opponent)
- Match states (WaitingForFaceOff, Playing, GoalScored, Paused, MatchEnded)
- Face-off system
- Overtime support (optional)
- Victory/defeat logic

**GoalTrigger.cs** - Goal detection
- Detects puck entering goal
- Notifies GameManager to update score
- Visual/audio feedback support
- Editor visualization (red = defend, green = attack)

---

## Unity Setup (Step-by-Step)

### Step 1: Create GameManager GameObject

1. **In Hierarchy**, right-click → **Create Empty**
2. **Rename it:** `GameManager`
3. **Add Component:** Search for "Game Manager" and add it
4. **Configure in Inspector:**
   - Match Duration: `300` (5 minutes)
   - Enable Overtime: ☐ (unchecked for now)
   - Player Team Name: "Player Team"
   - Opponent Team Name: "Opponent Team"
   - Center Ice Position: `(0, 0)` (adjust to your rink center)
   - Face Off Delay: `2`

---

### Step 2: Add GoalTrigger to Goals

You should already have goal GameObjects in your scene. Let's add the trigger component:

**For LEFT goal (Player Defends This):**
1. **Select your left goal** in Hierarchy
2. **Add Component:** "Goal Trigger"
3. **In Inspector:**
   - **Is Player Goal:** ✓ (CHECKED - player defends this)
   - Show Debug Messages: ✓ (checked)
4. **Add BoxCollider2D** if not already present:
   - Is Trigger: ✓ (MUST be checked!)
   - Size: Adjust to cover the goal opening (e.g., 3 x 2)

**For RIGHT goal (Player Attacks This):**
1. **Select your right goal** in Hierarchy
2. **Add Component:** "Goal Trigger"
3. **In Inspector:**
   - **Is Player Goal:** ☐ (UNCHECKED - opponent defends this)
   - Show Debug Messages: ✓ (checked)
4. **Add BoxCollider2D** if not already present:
   - Is Trigger: ✓ (MUST be checked!)
   - Size: Adjust to cover the goal opening

---

### Step 3: Verify Puck Has "Puck" Tag

GameManager needs to find the puck:

1. **Select your Puck** GameObject in Hierarchy
2. **In Inspector**, check the **Tag dropdown** (top)
3. If not set to "Puck":
   - Click Tag dropdown → select **"Puck"**
   - If "Puck" doesn't exist, click **Add Tag...** and create it

---

## Testing the System

### Quick Test:

1. **Press Play** ▶️
2. **Check Console** - You should see:
   ```
   === MATCH STARTING ===
   Player Team vs Opponent Team
   Match Duration: 5:00
   Face-off at center ice...
   Puck drop! Play begins!
   ```

3. **Shoot the puck into a goal** (use SHOOT button or cheat: manually drag puck into goal)
4. **Console should show:**
   ```
   🚨 GOAL! PLAYER scored in Goal_Right
   Player Team scores! (1-0)
   Face-off at center ice...
   ```

5. **Wait 5 minutes** (or change Match Duration to 10 seconds for quick test)
6. **Console shows:**
   ```
   === TIME EXPIRED ===
   === MATCH ENDED ===
   Final Score: Player Team 1 - 0 Opponent Team
   Result: Player Team WINS! 🏆
   ```

---

## Match Flow Sequence

Here's what happens during a match:

```
1. Match Starts
   ↓
2. Face-Off at Center Ice (2 second delay)
   ↓
3. Puck Drop → Match State = PLAYING
   ↓
4. Timer Counts Down (5:00 → 0:00)
   ↓
5. [If Goal Scored]
   - Update Score
   - State = GoalScored
   - Wait 2 seconds
   - Face-Off at Center
   - State = Playing
   ↓
6. [When Timer Reaches 0:00]
   - Check Score:
     - If Tied → Overtime (if enabled) or Tie
     - If Not Tied → Winner Determined
   ↓
7. Match State = MatchEnded
```

---

## Events System (For UI)

GameManager fires events that you can subscribe to for UI updates:

```csharp
// In your UI script:
private void OnEnable()
{
    GameManager.Instance.OnScoreChanged += UpdateScoreDisplay;
    GameManager.Instance.OnTimerChanged += UpdateTimerDisplay;
    GameManager.Instance.OnGoalScored += ShowGoalCelebration;
    GameManager.Instance.OnMatchStateChanged += HandleStateChange;
}

private void UpdateScoreDisplay(int playerScore, int opponentScore)
{
    scoreText.text = $"{playerScore} - {opponentScore}";
}

private void UpdateTimerDisplay(float timeRemaining)
{
    timerText.text = GameManager.FormatTime(timeRemaining);
}
```

---

## Public API Reference

### GameManager Methods:

| Method | Description |
|--------|-------------|
| `GameManager.Instance` | Access singleton instance |
| `StartMatch()` | Start new match with face-off |
| `GoalScored(bool scoredByPlayer)` | Register a goal (called by GoalTrigger) |
| `PauseMatch()` | Pause the game (sets Time.timeScale = 0) |
| `ResumeMatch()` | Resume from pause |
| `RestartMatch()` | Reload scene to restart |
| `FormatTime(float seconds)` | Convert seconds to "MM:SS" format |

### GameManager Properties:

| Property | Type | Description |
|----------|------|-------------|
| `CurrentState` | MatchState | Current match state |
| `PlayerScore` | int | Player team score |
| `OpponentScore` | int | Opponent team score |
| `TimeRemaining` | float | Seconds remaining |
| `IsOvertime` | bool | Is match in overtime? |

---

## Advanced Configuration

### Enable Overtime:

In GameManager Inspector:
- Enable Overtime: ✓ (checked)
- Overtime Duration: `60` (1 minute sudden death)

**How it works:**
- If match ends tied in regulation → 1:00 overtime begins
- First goal in overtime = instant win
- If overtime ends tied → match ends in tie

### Custom Team Names:

In GameManager Inspector:
- Player Team Name: "Your Team Name"
- Opponent Team Name: "Rival Team Name"

These appear in console logs and can be displayed in UI.

---

## Troubleshooting

**Goals not being detected:**
- ✓ Check GoalTrigger component is on goal GameObject
- ✓ Check goal has Collider2D with "Is Trigger" checked
- ✓ Check puck has "Puck" tag
- ✓ Check match state is "Playing" (not Paused or MatchEnded)

**Timer not counting down:**
- ✓ Check GameManager exists in scene
- ✓ Check match state is "Playing"
- ✓ Check Time.timeScale = 1 (not paused)

**Face-off doesn't work:**
- ✓ Check Center Ice Position is correct (where puck should spawn)
- ✓ Check puck GameObject is found (has "Puck" tag)

---

## Next Steps (Optional Enhancements)

### UI Integration (Issue #48 - HUD & In-Game UI):
- Display timer at top center
- Display score at top center  
- Show "GOAL!" overlay when scored
- Victory/defeat screen

### Visual Polish:
- Goal celebration particle effects
- Camera shake on goals
- Slow-motion replay
- Goal horn sound effect

### Gameplay Features:
- Penalty system (penalties send player to box, 5v4)
- Shootout mode (if tied after overtime)
- Period breaks (3 periods instead of continuous)

---

## 3D Transfer Ready ✅

**100% transferable to 3D!**
- GameManager = pure logic (no 2D dependencies)
- GoalTrigger = works with Collider (2D or 3D)
- Just change `Collider2D` → `Collider` and `OnTriggerEnter2D` → `OnTriggerEnter`

---

**Your match system is ready!** Press Play and score some goals! 🏒🥅
