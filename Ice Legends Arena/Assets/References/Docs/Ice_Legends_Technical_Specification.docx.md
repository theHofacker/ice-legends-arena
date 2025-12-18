# **ICE LEGENDS: ARENA**

Technical Specification  
*Unity Implementation Guide*

# **1\. PROJECT SETUP**

## **1.1 Unity Configuration**

* **Unity Version:** 2022.3 LTS (Long Term Support)  
* **Render Pipeline:** Universal Render Pipeline (URP) 2D  
* **Target Platforms:** iOS (minimum iOS 12), Android (minimum API 24\)  
* **Color Space:** Linear (for better lighting)

## **1.2 Required Packages**

* 2D Sprite (built-in)  
* 2D Animation (for character sprites)  
* Input System (new input system)  
* TextMeshPro (for UI text)  
* Cinemachine (for camera control)  
* Photon Unity Networking 2 (PUN2) \- for multiplayer in v1.1

## **1.3 Project Structure**

Assets/

├── \_Scenes/

│   ├── MainMenu.unity

│   ├── Gameplay.unity

│   └── LoadingScreen.unity

├── Scripts/

│   ├── Managers/          (GameManager, InputManager, etc.)

│   ├── Player/            (Movement, shooting, checking)

│   ├── Characters/        (Abilities, stats)

│   ├── Equipment/         (Equipment system)

│   ├── AI/                (Opponent AI)

│   ├── UI/                (Menus, HUD)

│   └── Utilities/         (Helper classes)

├── Prefabs/

│   ├── Characters/        (8 character prefabs)

│   ├── Puck.prefab

│   ├── IceRink.prefab

│   └── UI/                (Buttons, HUD elements)

├── Art/

│   ├── Sprites/           (Character sprites, puck, rink)

│   ├── VFX/               (Particle effects)

│   └── UI/                (Buttons, icons)

├── Audio/

│   ├── SFX/               (Shot sounds, check sounds)

│   └── Music/             (Menu, gameplay BGM)

└── Resources/             (ScriptableObjects, config)

# **2\. CORE SYSTEMS ARCHITECTURE**

## **2.1 Input System**

**Implementation:** Unity's new Input System with touch controls

**Key Components:**

* **Virtual Joystick:** Left side for movement (8-directional)  
* **Context Buttons:** Right side, change based on possession state  
* **Button States:** Tap, Hold, Hold+Release (timing), Swipe-off (fakes)

**C\# Pseudocode:**

public class InputManager : MonoBehaviour

{

    public enum PossessionState { Offense, Defense }

    private PossessionState currentState;

    // Right button mappings change based on state

    void UpdateButtonContext()

    {

        if (PlayerHasPuck)

        {

            button1.SetAction(ShootAction);

            button2.SetAction(PassAction);

            button3.SetAction(DekeAction);

        }

        else

        {

            button1.SetAction(CheckAction);

            button2.SetAction(SwitchAction);

            button3.SetAction(DefenseAction);

        }

    }

}

## **2.2 Perfect Timing System**

Critical mechanic for slapshots, perfect checks, and one-timers.

**Implementation:**

public class TimingMeter : MonoBehaviour

{

    public float chargeDuration \= 1.0f; // Total charge time

    public float greenZoneStart \= 0.8f;  // 80% of charge

    public float greenZoneEnd \= 1.0f;    // 100% of charge

    public float redZoneStart \= 1.0f;    // Overcharged

    private float currentCharge \= 0f;

    public TimingResult CheckTiming()

    {

        float normalized \= currentCharge / chargeDuration;

        

        if (normalized \>= greenZoneStart && normalized \<= greenZoneEnd)

            return TimingResult.Perfect;  // Green zone

        else if (normalized \> greenZoneEnd)

            return TimingResult.Overcharged;  // Red zone

        else

            return TimingResult.Weak;  // Yellow zone

    }

}

## **2.3 Puck Physics**

**Implementation:** Physics2D with custom physics material

* **Friction:** 0.1 (slides on ice)  
* **Bounce:** 0.7 (bounces off boards)  
* **Mass:** 0.17 kg (real puck mass)  
* **Gravity:** 0 (puck stays on ice, unless saucer pass)

# **3\. CHARACTER SYSTEM**

## **3.1 Character Data Structure**

**Use ScriptableObjects for data-driven design:**

\[CreateAssetMenu(fileName \= "New Character", menuName \= "Ice Legends/Character")\]

public class CharacterData : ScriptableObject

{

    public string characterName;

    public CharacterType type; // Elementalist, Berserker, etc.

    public Position position;  // Forward, Defense, Center, Wing

    \[Header("Base Stats")\]

    public float shotPower \= 1.0f;      // 0.5 \- 1.5 range

    public float speed \= 1.0f;          // 0.5 \- 1.5 range

    public float checking \= 1.0f;       // 0.5 \- 1.5 range

    public float accuracy \= 1.0f;       // 0.5 \- 1.5 range

    \[Header("Ability")\]

    public AbilityData ability;

    public float abilityCooldown \= 30f; // Seconds

    \[Header("Visual")\]

    public Sprite characterSprite;

    public RuntimeAnimatorController animController;

}

## **3.2 Ability System**

Each character has a unique ability with cooldown:

public abstract class Ability : MonoBehaviour

{

    public float cooldown \= 30f;

    private float cooldownTimer \= 0f;

    public bool CanUseAbility() \=\> cooldownTimer \<= 0f;

    public void ActivateAbility()

    {

        if (\!CanUseAbility()) return;

        

        ExecuteAbility();  // Override this in subclasses

        cooldownTimer \= cooldown;

    }

    protected abstract void ExecuteAbility();

}

# **4\. EQUIPMENT SYSTEM**

## **4.1 Equipment Data**

public enum EquipmentSlot { Stick, Skates, Gloves, Helmet }

public enum EquipmentRarity { Common, Rare, Epic, Legendary }

\[CreateAssetMenu(fileName \= "New Equipment", menuName \= "Ice Legends/Equipment")\]

public class EquipmentData : ScriptableObject

{

    public string equipmentName;

    public EquipmentSlot slot;

    public EquipmentRarity rarity;

    public float shotPowerBonus \= 0f;

    public float speedBonus \= 0f;

    public float checkingBonus \= 0f;

    public float accuracyBonus \= 0f;

    public Sprite icon;

    public GameObject visualEffect; // For rare+ equipment

}

## **4.2 Equipment Manager**

Handles equipping and calculating stat bonuses:

public class EquipmentManager : MonoBehaviour

{

    private Dictionary\<EquipmentSlot, EquipmentData\> equipped \= new();

    public void EquipItem(EquipmentData equipment)

    {

        equipped\[equipment.slot\] \= equipment;

        RecalculateStats();

    }

    public float GetTotalShotPowerBonus()

    {

        return equipped.Values.Sum(e \=\> e.shotPowerBonus);

    }

    // Similar for other stats...

}

# **5\. AI SYSTEM**

## **5.1 AI Behavior States**

Use finite state machine for AI opponents:

public enum AIState

{

    Idle,

    ChasePuck,      // Move toward puck

    AttackGoal,     // Have puck, move to goal

    DefendGoal,     // Protect own goal

    PassToTeammate, // Look for open teammate

    CheckOpponent   // Body check opponent with puck

}

## **5.2 AI Decision Making**

Simple decision tree for MVP:

* **If has puck:** Look for shot opportunity → Pass if covered → Shoot if open  
* **If opponent has puck:** Chase → Try to check/poke  
* **If no one has puck:** Chase puck → Get possession  
* **If far from play:** Position defensively

# **6\. UI SYSTEM**

## **6.1 HUD Layout**

| UI Element | Details |
| ----- | ----- |
| Top Bar | Score (Team1 vs Team2), Timer (5:00 countdown) |
| Bottom Left | Virtual joystick (movement) |
| Bottom Right | Context buttons (3 buttons: SHOOT/CHECK, PASS/SWITCH, DEKE/DEFENSE) |
| Top Right | Character portraits (4 players), ability meters |
| Center Overlay | Timing meter (when charging shot/check), goal celebration |

# **7\. PERFORMANCE OPTIMIZATION**

## **7.1 Mobile Optimization**

* **Target:** 60 FPS on mid-range devices (iPhone 12, Galaxy S21)  
* **Sprite Atlasing:** Combine all character sprites into atlas  
* **Object Pooling:** Pool VFX particles (ice spray, impact effects)  
* **Draw Call Batching:** Use SpriteRenderer batching, minimize state changes  
* **Physics Optimization:** Limit collision checks, use layer masks

## **7.2 Asset Guidelines**

* **Sprite Resolution:** 256x256 for characters, 128x128 for UI  
* **Texture Compression:** ASTC for all platforms  
* **Audio Format:** Vorbis for music, ADPCM for SFX  
* **Build Size Target:** \< 150 MB

# **8\. DEVELOPMENT MILESTONES**

| Week | System | Tasks |
| ----- | ----- | ----- |
| 1-2 | Movement | Virtual joystick, player movement, ice physics |
| 3-4 | Shooting | Wrist shot, slapshot, perfect timing system, puck physics |
| 5-6 | Passing | Basic pass, saucer pass, fake pass, one-timers |
| 7-8 | Checking | Poke check, body check, perfect check, glass hits |
| 9-10 | Characters | 8 characters, 1 ability each, stat system |
| 11-12 | Equipment | Equipment slots, stat bonuses, rarity system |
| 13-14 | AI | Basic AI opponents, state machine, decision tree |
| 15-16 | Polish | Testing, balance, bug fixes, MVP complete |

# **9\. TESTING CHECKLIST**

## **9.1 Core Mechanics Testing**

* Movement feels responsive (no lag)  
* Perfect timing windows are consistent (green zone)  
* Puck physics feel realistic (sliding, bouncing)  
* One-timers work smoothly (receive \+ shoot)  
* Glass hits trigger properly near boards

## **9.2 Character Balance Testing**

* All characters feel viable (no OP characters)  
* Abilities have impact but not game-breaking  
* Skill \> Stats (good player beats better team)  
* Equipment bonuses are meaningful but not mandatory

## **9.3 Performance Testing**

* 60 FPS on target devices  
* No frame drops during abilities  
* Build size \< 150 MB  
* Battery drain is acceptable (\< 10%/hour)

# **10\. NEXT STEPS**

1. Set up Unity project with URP 2D template  
2. Install required packages (Input System, 2D Animation)  
3. Create folder structure as outlined  
4. Begin Week 1-2: Movement & Controls with Claude Code  
5. Download placeholder art from Unity Asset Store

**\--- END OF TECHNICAL SPECIFICATION \---**