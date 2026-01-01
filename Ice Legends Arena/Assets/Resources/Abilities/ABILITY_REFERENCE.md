# Ability Reference

This document defines the 8 special abilities for Ice Legends Arena characters.
Use this as a guide when creating AbilityData ScriptableObject assets in Unity.

## How to Create Ability Assets in Unity:
1. In Project window, navigate to `Assets/Resources/Abilities/`
2. Right-click → Create → Ice Legends → Ability Data
3. Name it after the ability (e.g., "MeteorStrike")
4. Fill in data according to this reference

---

## Ability Roster

### 1. **Meteor Strike** 🔥
**Character:** Blaze the Elementalist
**Type:** Offensive
**Cooldown:** 40 seconds
**Duration:** Instant (explosion)

**Description:**
"Summons a flaming meteor that crashes onto the puck, launching it toward the opponent's goal with explosive force. Knocks back nearby opponents."

**Mechanics:**
- Tap character portrait to activate
- Meteor targets puck's current location
- Puck gains massive velocity toward goal (+50 force)
- Opponents within 5 units are knocked back
- Visual: Fire particles, screen shake

---

### 2. **Rampage Mode** ⚔️
**Character:** Ragnar the Berserker
**Type:** Offensive
**Cooldown:** 35 seconds
**Duration:** 5 seconds

**Description:**
"Enters a berserker rage, gaining +50% speed and +100% checking power. All hits during rampage stun opponents for 2 seconds."

**Mechanics:**
- Tap character portrait to activate
- Speed multiplier: 1.5x
- Checking multiplier: 2.0x
- All body checks stun opponents (2s)
- Visual: Red aura, angry face icon

---

### 3. **Temporal Rewind** ⏰
**Character:** Chronos the Chronomancer
**Type:** Control
**Cooldown:** 35 seconds
**Duration:** Instant (rewind effect)

**Description:**
"Rewinds the puck 2 seconds back in time, returning it to its previous position. Can prevent opponent goals or reset failed shots."

**Mechanics:**
- Tap character portrait to activate
- Puck teleports to position from 2 seconds ago
- Time.timeScale slows to 0.5 for 0.5s (dramatic effect)
- Visual: Purple time particles, clock rewind animation

---

### 4. **Phantom Step** 🌑
**Character:** Shadow the Assassin
**Type:** Utility
**Cooldown:** 25 seconds
**Duration:** 3 seconds

**Description:**
"Becomes invisible and gains +50% speed. Can steal puck from behind without penalty. Leaves shadow clone decoy at activation point."

**Mechanics:**
- Tap character portrait to activate
- Player sprite alpha: 30% (semi-invisible)
- Speed multiplier: 1.5x
- Can steal puck without triggering checks
- Spawns shadow clone decoy (fades after 2s)
- Visual: Shadow particles, afterimages

---

### 5. **Holy Barrier** 🛡️
**Character:** Valora the Paladin
**Type:** Defensive
**Cooldown:** 30 seconds
**Duration:** 4 seconds

**Description:**
"Creates a holy wall of light in front of the paladin that blocks all shots and pucks. Opponents cannot pass through."

**Mechanics:**
- Tap character portrait to activate
- Spawns barrier prefab 2 units in front of player
- Barrier size: 6 units wide, 3 units tall
- Blocks puck physics (acts like wall)
- Destroys after 4 seconds
- Visual: Golden light wall, sparkles

---

### 6. **Carbuncle Call** 🦊
**Character:** Aria the Summoner
**Type:** Support
**Cooldown:** 30 seconds
**Duration:** 8 seconds

**Description:**
"Summons a magical Carbuncle creature that follows the puck and blocks/redirects it. Acts as a mobile obstacle for opponents."

**Mechanics:**
- Tap character portrait to activate
- Spawns Carbuncle AI creature at midfield
- Carbuncle chases puck (slower than players)
- Puck bounces off Carbuncle (like a wall)
- Opponents can push Carbuncle (but it's heavy)
- Visual: Cute fox-like creature, magical sparkles

---

### 7. **Trick Shot** 🔫
**Character:** McCree the Gunslinger
**Type:** Offensive
**Cooldown:** 28 seconds
**Duration:** Instant (shot effect)

**Description:**
"Next shot ricochets off boards at sharp angles, making it nearly impossible to predict. Can curve around defenders."

**Mechanics:**
- Tap character portrait to activate (arms next shot)
- Next SHOOT press triggers trick shot
- Puck bounces off boards at 2x normal angle
- Ignores normal physics damping on bounce
- Can hit goal from extreme angles
- Visual: Bullet trail effects, ricochet spark

---

### 8. **Shapeshift** 🐺
**Character:** Fenrir the Druid
**Type:** Utility
**Cooldown:** 20 seconds (toggle)
**Duration:** Permanent until toggled again

**Description:**
"Transform into different animal forms: Bear (tank), Cheetah (speed), Hawk (jump/dodge). Each form has different stat bonuses."

**Forms:**
- **Bear:** +40% checking, +20% shot power, -20% speed
- **Cheetah:** +50% speed, +20% puck control, -30% checking
- **Hawk:** +30% speed, can "jump" over checks (brief invincibility)

**Mechanics:**
- Tap character portrait to cycle forms
- Form changes are instant
- Cooldown only applies when changing forms
- Visual: Transformation particle burst, different sprite/model per form

---

## Ability Balance Notes

- **Cooldown Range:** 20-40 seconds (based on power level)
- **Offensive abilities:** Higher cooldowns (35-40s) due to direct impact
- **Utility abilities:** Lower cooldowns (20-28s) for positioning/flexibility
- **Defensive abilities:** Medium cooldowns (30s) for balance
- **Duration:** Most abilities are instant or 3-5 seconds to keep gameplay fast

## Future Ability Ideas (Post-MVP)
- Ice Wall (creates temporary barrier)
- Lightning Strike (stuns area)
- Teleport Dash
- Puck Magnet (attracts puck to you)
- Time Slow Zone
