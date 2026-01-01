# Character Data System - Setup Guide

This guide explains how to use the Character Data ScriptableObject system in Unity.

## ✅ What's Already Created

1. ✅ **CharacterData.cs** - ScriptableObject for character stats
2. ✅ **AbilityData.cs** - ScriptableObject for abilities
3. ✅ **CharacterStatsApplier.cs** - Component that applies stats to gameplay
4. ✅ **Resources/Characters/** - Folder for character assets
5. ✅ **Resources/Abilities/** - Folder for ability assets
6. ✅ **CHARACTER_STATS_REFERENCE.md** - Stats for all 8 characters
7. ✅ **ABILITY_REFERENCE.md** - Data for all 8 abilities

---

## 📝 Step 1: Create Ability Data Assets (Do This First!)

Abilities need to be created before characters (since characters reference abilities).

### In Unity Editor:

1. **Navigate to:** `Assets/Resources/Abilities/`
2. **Right-click** in Project window → **Create → Ice Legends → Ability Data**
3. **Name it:** "MeteorStrike" (or ability name from ABILITY_REFERENCE.md)
4. **Fill in fields** using reference document:
   - Ability Name: "Meteor Strike"
   - Description: Copy from reference
   - Cooldown: 40
   - Duration: 0 (instant)
   - Ability Type: Offensive
   - Icon: (optional for now)
   - Particle Effect: (optional for now)

**Repeat for all 8 abilities:**
- MeteorStrike
- RampageMode
- TemporalRewind
- PhantomStep
- HolyBarrier
- CarbuncleCall
- TrickShot
- Shapeshift

---

## 📝 Step 2: Create Character Data Assets

### In Unity Editor:

1. **Navigate to:** `Assets/Resources/Characters/`
2. **Right-click** in Project window → **Create → Ice Legends → Character Data**
3. **Name it:** "Elementalist_Blaze" (or character name)
4. **Fill in fields** using CHARACTER_STATS_REFERENCE.md:

**Example for Blaze the Elementalist:**
- Character Name: "Blaze the Elementalist"
- Character Class: "Elementalist"
- Position: Center
- Shot Power: 1.5
- Speed: 1.0
- Checking: 0.7
- Accuracy: 1.2
- Puck Control: 1.0
- Ability: **Drag MeteorStrike asset here**
- Character Sprite: (leave empty for now - will use default)
- Character Color: Orange (#FF4500)
- Unlocked By Default: ✓ (checked)

**Repeat for all 8 characters:**
- Elementalist_Blaze
- Berserker_Ragnar
- Chronomancer_Chronos
- ShadowAssassin_Shadow
- Paladin_Valora
- Summoner_Aria
- Gunslinger_McCree
- Druid_Fenrir

---

## 📝 Step 3: Apply Character Data to Players

### Method A: Existing Players (Manual Setup)

1. **Select a player GameObject** in Hierarchy (e.g., "Player (1)")
2. **Add Component:** Search for "Character Stats Applier"
3. **Drag a CharacterData asset** into the "Character Data" field
4. **Check "Auto Apply On Start"** (enabled by default)

**✅ Stats will automatically apply when game starts!**

---

### Method B: Assign All 5 Players Different Characters

Your team has 5 players. Give each one a different character:

**Example Balanced Team:**
- Player (1): Chronos (Center - playmaker)
- Player (2): Shadow (Right Wing - speed)
- Player (3): Gunslinger (Left Wing - sniper)
- Player (4): Valora (Left Defense - tank)
- Player (5): Summoner (Right Defense - support)

**Steps:**
1. Select **Player (1)**
2. Add Component → **Character Stats Applier**
3. Drag **Chronomancer_Chronos** asset to Character Data field
4. Repeat for Players 2-5 with different characters

---

### Method C: AI Opponents

Opponents can also have CharacterData!

1. Select **Opponent_Center** (or any AI opponent)
2. Add Component → **Character Stats Applier**
3. Drag a CharacterData asset (e.g., Berserker_Ragnar)
4. AI will use those stats automatically

**Benefits:**
- Opponents have varied playstyles
- Some opponents are fast, some are tanks
- More interesting gameplay

---

## 🎮 How Stats Affect Gameplay

Once CharacterStatsApplier is added, stats automatically modify gameplay:

| Stat | Affects | Example |
|------|---------|---------|
| **Shot Power** | Shot force, pass power | Blaze (1.5x) shoots 50% harder than default |
| **Speed** | Movement speed | Shadow (1.3x) skates 30% faster |
| **Checking** | Body check force | Ragnar (1.5x) knocks back 50% harder |
| **Accuracy** | Aim spread | Gunslinger (1.3x) has tighter aim cone |
| **Puck Control** | Possession radius | Chronos (1.2x) picks up puck 20% easier |

**Example:**
- **Default shot power:** 25 force
- **Blaze (1.5x shot power):** 25 × 1.5 = **37.5 force**
- **Ragnar (1.3x shot power):** 25 × 1.3 = **32.5 force**
- **Shadow (0.9x shot power):** 25 × 0.9 = **22.5 force**

---

## 🔄 Testing the System

### Quick Test:

1. Create **one CharacterData** asset (e.g., Berserker with 1.5x checking)
2. Add **CharacterStatsApplier** to Player (1)
3. Assign the Berserker asset
4. **Play the game**
5. Press DEKE to body check an opponent
6. **Result:** They should fly back 50% farther than normal!

### Console Output:

When game starts, you should see:
```
Applied Ragnar the Berserker stats to Player (1)
  Speed: 0.8x (final: 3.2)
  Shot Power: 1.3x, Accuracy: 0.9x
  Checking: 1.5x (final: 45)
```

---

## 🚀 Benefits of This System

### For 2D Development:
- ✅ Easy to balance (tweak stats in Inspector)
- ✅ Add new characters without code changes
- ✅ Designers can create characters

### For 3D Transfer:
- ✅ **100% of stats logic transfers** (shot power, speed, etc. are universal)
- ✅ **Only visual references change** (swap sprite → model)
- ✅ **CharacterStatsApplier works in 3D** (no code changes needed)
- ✅ **All 8 characters transfer instantly**

---

## 🛠️ Advanced: Creating Custom Characters

Want to create your own 9th character?

1. Create new **AbilityData** asset (if custom ability)
2. Create new **CharacterData** asset
3. Set unique stats (experiment with different multipliers!)
4. Apply to a player
5. Test and balance!

**Example Custom Character:**
- Name: "Titan"
- Shot Power: 1.6 (highest!)
- Speed: 0.7 (slowest)
- Checking: 1.6 (devastating)
- Accuracy: 0.8 (wild shots)
- Playstyle: Ultra tank - slow but unstoppable

---

## 📌 Next Steps

1. ✅ Create all 8 ability assets
2. ✅ Create all 8 character assets
3. ✅ Assign characters to Player (1-5)
4. ✅ Test gameplay with different character combinations
5. ⏭️ Implement Ability System Framework (Issue #30)
6. ⏭️ Add roster selection screen (Issue #52)

---

## 🐛 Troubleshooting

**Stats don't apply:**
- Check Console for "Applied [Character] stats" message
- Ensure Auto Apply On Start is checked
- Verify CharacterData asset is assigned

**Character looks wrong:**
- Character Color is applied automatically
- Character Sprite is only applied if you set one in the asset

**AI doesn't use stats:**
- Make sure CharacterStatsApplier is on AI GameObject
- AI has different component names (AIController, TeammateController)
- Check Console for which components were found

---

## 💡 Pro Tips

1. **Start with 3 characters** - Don't create all 8 at once, test incrementally
2. **Use extreme stats first** - Create a 2.0x speed character to see the effect clearly
3. **Test in pairs** - Create a tank (high checking) vs speed demon (high speed)
4. **Balance later** - Get the system working first, fine-tune stats after

---

**This system is ready to use! Create your first character and see it in action! 🎮**
