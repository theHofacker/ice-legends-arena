# **ICE LEGENDS: ARENA**

Game Design Document v2.0  
*5v5 Fantasy Hockey Mobile Game*

# **1\. EXECUTIVE SUMMARY**

**Project Overview**

Ice Legends: Arena is a fast-paced fantasy hockey mobile game combining arcade action with deep skill-based mechanics. Players control teams of 5 unique fantasy characters (plus AI goalie), each with special abilities, competing in 5-minute 5v5 hockey matches. The game features character-based superpowers, equipment progression, formation strategy, and timing-based mechanics that create a high skill ceiling while remaining accessible to beginners.

**Core Innovation: 5v5 with Formation Strategy**

Unlike competitors who use 3v3 or 4v4, Ice Legends embraces authentic 5v5 hockey (5 skaters \+ goalie). This enables real formation strategy (1-2-2 defensive, 2-1-2 balanced, 1-3-1 offensive, 2-2-1 shutdown), team composition meta (which 5 of 8 characters?), and creates competitive depth similar to traditional sports games while maintaining mobile-friendly controls.

**Target Platform**

* Mobile (iOS & Android)  
* 2.5D isometric perspective (2D sprites with 3D depth)  
* Puck-following camera (inspired by Mini Basketball)  
* Unity Engine (2022.3 LTS)

**Development Timeline**

* MVP Target: 3-4 months (20-30 hours/week)  
* Team: Solo developer \+ freelance artists (Fiverr)  
* Tools: Unity, Claude Code, AI art generation \+ manual editing

**Key Differentiators**

* 5v5 gameplay with formation strategy (not 3v3/4v4)  
* Character-based superpowers (not team-wide buffs)  
* Equipment system extends character lifespan  
* Only 8 core characters (each feels special)  
* Timing-based perfect mechanics (slapshots, one-timers, checks)  
* Glass hit board checks (unique to hockey)  
* Puck-following camera (dynamic, mobile-optimized)  
* Ethical F2P (no pay-to-win)

# **2\. CORE GAMEPLAY**

## **2.1 Game Flow**

* Match Format: 5-minute 5v5 hockey (5 skaters \+ AI goalie)  
* Teams: Select 5 characters from 8 available \+ choose formation  
* Positions: Left Wing, Right Wing, Center, Left Defense, Right Defense  
* Modes: Single-player vs AI, PvP (multiplayer in v1.1)  
* Rules: Loose realistic hockey rules (no offsides for MVP, optional penalty system)

## **2.2 Formation System**

Choose formation before each match. Formation determines starting positions and AI behavior:

| Formation | Style | Best For |
| ----- | ----- | ----- |
| **1-2-2** | Conservative/Defensive | Protecting lead, beginners |
| **2-1-2** | Balanced/Standard | All-around play, default |
| **1-3-1** | Aggressive/Offensive | Chasing goals, high-risk |
| **2-2-1** | Defensive Shutdown | Zone control, counter-attacks |

**Formation Visualization Example (1-2-2):**

        LW          RW  
            C  
      LD        RD  
            G

## **2.3 Camera System (Puck-Following)**

Inspired by Mini Basketball, the camera follows the puck (not the player):

* **Dynamic Follow:** Camera smoothly tracks puck movement  
* **Zoom Level:** Shows \~60-70% of rink (not full ice)  
* **Predictive Lead:** Camera slightly anticipates puck direction  
* **Mobile Optimized:** Players appear larger, easier to control  
* **Fog of War:** Can't see full rink \= strategic depth

**What You Can See:**

* Player with puck (center screen)  
* Nearby teammates (2-3 players)  
* Immediate defenders (checking distance)  
* Open passing lanes

**What You Can't See:**

* Players far down ice (must anticipate or use minimap)  
* Full defensive positioning (creates breakaway opportunities)

## **2.4 Control Scheme**

The control system uses context-sensitive buttons that change based on possession:

| Control Zone | Function |
| ----- | ----- |
| **Left Joystick** | Movement (skating direction) |
| **Right Buttons (Offense)** | **SHOOT:** Tap \= wrist shot | Hold \= slapshot | Hold \+ green release \= perfect slapshot **PASS:** Tap \= quick pass | Hold \= saucer pass | Hold \+ swipe off \= fake pass **DEKE:** Tap \= quick deke | Hold \= speed burst |
| **Right Buttons (Defense)** | **CHECK:** Tap \= poke check | Hold \= body check | Hold \+ green release \= perfect check **SWITCH:** Tap \= nearest player | Hold \= last defender | Multiple taps \= cycle through 5 players **DEFENSE:** Hold \= auto-position |
| **Character Ability** | Tap character portrait when meter is full |

**Key Mechanic: Perfect Timing Windows**

Many actions have a \~1 second charge with a green zone timing window:

* Yellow zone (early): Weak execution  
* Green zone (perfect): Maximum power/effect  
* Red zone (late): Overcharged/failed

# **3\. ROSTER SELECTION & TEAM COMPOSITION**

## **3.1 Pre-Game Team Setup**

Before each match, players select 5 characters from the 8-character roster. The remaining 3 characters sit on the bench (substitutions in v1.1).

**Example Starting Lineup:**

1. **Left Wing:** Elementalist (high shot power, meteor strike ability)  
2. **Right Wing:** Shadow Assassin (speed specialist, phantom step)  
3. **Center:** Chronomancer (playmaker, temporal rewind)  
4. **Left Defense:** Berserker (enforcer, rampage mode)  
5. **Right Defense:** Paladin (shutdown, holy barrier)

**Bench (Substitutions in v1.1):**

* Summoner (tactical option)  
* Gunslinger (offensive spark plug)  
* Druid (flex position)

## **3.2 Team Composition Meta**

Which 5 characters to pick creates strategic depth:

**Balanced Team (Recommended for Beginners):**

* 2 Forwards with high shot power  
* 1 Playmaking center  
* 2 Strong defensemen

**Speed Team (High-Risk Offense):**

* 3 Fast forwards (Shadow Assassin, Chronomancer, Druid)  
* 1 Defenseman  
* Relies on breakaways and speed

**Tank Team (Defensive Shutdown):**

* 3 Defensemen (Berserker, Paladin, Summoner)  
* 2 Forwards  
* Focus on counter-attacks and physicality

# **4\. CHARACTER ROSTER**

Ice Legends features 8 unique fantasy characters. See separate Character Reference document for complete ability details.

| Character | Position | Type | Ability | Cooldown |
| ----- | ----- | ----- | ----- | :---: |
| **Elementalist** | Forward | Mage | Meteor Strike | 30s |
| **Chronomancer** | Center | Time | Temporal Rewind | 35s |
| **Berserker** | Defense | Tank | Rampage Mode | 40s |
| **Shadow Assassin** | Wing | Speed | Phantom Step | 25s |
| **Paladin** | Defense | Support | Holy Barrier | 45s |
| **Summoner** | Center | Tactical | Carbuncle Call | 35s |
| **Gunslinger** | Wing | Ranged | Trick Shot | 30s |
| **Druid** | Flex | Adaptive | Shapeshift | 40s |

# **5\. EQUIPMENT & PROGRESSION**

Equipment extends character lifespan and provides stat bonuses. Each character has 4 equipment slots: Stick, Skates, Gloves, Helmet. See full GDD v1.0 for complete equipment specifications.

# **6\. MONETIZATION & F2P STRATEGY**

Ethical free-to-play model focused on cosmetics and convenience, not pay-to-win. See full GDD v1.0 for detailed monetization strategy.

# **7\. DEVELOPMENT ROADMAP**

## **7.1 MVP Scope (3-4 Months)**

* 5v5 gameplay with 4 formations  
* All 8 characters with 1 ability each  
* Puck-following camera system  
* Roster selection (pick 5 of 8\)  
* Formation selector with pre-game screen  
* Basic equipment system  
* Single-player vs AI  
* Core progression (card packs, leveling)

## **7.2 Post-MVP Features (v1.1+)**

* Multiplayer PvP (5v5 online)  
* Substitution system (swap bench players)  
* Fatigue/stamina mechanics  
* Penalties and power plays (5v4)  
* 3 ability variants per character  
* Tournament mode  
* Minimap overlay (optional)

# **8\. CONCLUSION**

Ice Legends: Arena combines authentic 5v5 hockey gameplay with mobile-optimized controls and deep strategic elements. The formation system, roster composition meta, and puck-following camera create a competitive experience that scales from casual to esports-level play.

By embracing 5v5 instead of simplified 3v3/4v4, Ice Legends captures the tactical depth of real hockey while maintaining the accessibility and fast-paced action expected from mobile games. The 8-character roster with equipment progression ensures long-term player engagement without the character bloat that plagues competitors.

**\--- END OF DOCUMENT \---**