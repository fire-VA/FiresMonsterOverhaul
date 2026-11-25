using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;

namespace FiresMonsterOverhaul
{
    // Fire 10-20 config cleanup (reused from FAP 1.1.279 Managers\FapConfigUi.cs): how FMO's settings show in the settings UI,
    // in ONE table. Nothing here changes a cfg section, key, default or value: each entry gets a ConfigurationManagerAttributes
    // tag (read by Core's settings UI and by ConfigurationManager) with its category, plain name, one-line description and
    // visibility:
    //   Shown    - the main on/off feature toggles (FMO has no keybinds)
    //   Advanced - everything else (scaling numbers, per-monster / per-boss tuning, server lock)
    //   Hidden   - none today (FMO has no UI geometry, bookkeeping or retired keys in its cfg)
    // Category merges: [07 - Champions] shows under [05 - Tiers], [09 - Veteran Abilities] under [03 - Abilities],
    // [11 - Boss Scaling] under [10 - Bosses]. ServerSync's own attribute is kept as Prior: its ReadOnly is the server lock.
    // Every bind has an explicit "Section|Key" rule (no loop-generated binds in FMO); an entry without a rule falls back to
    // Advanced and is logged as a warning.
    internal static class MonsterConfigUi
    {
        private enum Show { Shown, Advanced, Hidden }

        private sealed class Rule
        {
            public Show Show; public string Category, Name, Desc; public int? Order; public Func<bool> Grey;
            public Rule(Show show, string category = null, string name = null, string desc = null, int? order = null, Func<bool> grey = null)
            { Show = show; Category = category; Name = name; Desc = desc; Order = order; Grey = grey; }
        }

        // Read by name (ConfigurationManagerAttributes) by Core's CfgTags and by ConfigurationManager.
        internal sealed class ConfigurationManagerAttributes
        {
            public bool? Browsable;
            public bool? IsAdvanced;
            public int? Order;
            public string Category;
            public string DispName;
            public string Description;
            internal Func<bool> Grey;
            internal object Prior;   // ServerSync's own attribute object: its ReadOnly is the server lock
            public bool? ReadOnly
            {
                get
                {
                    bool locked = false;
                    try
                    {
                        if (Prior != null)
                        {
                            var t = Prior.GetType();
                            object v = (object)t.GetField("ReadOnly")?.GetValue(Prior) ?? t.GetProperty("ReadOnly")?.GetValue(Prior, null);
                            locked = v as bool? ?? false;
                        }
                    }
                    catch { }
                    bool grey = false;
                    try { grey = Grey != null && Grey(); } catch { }
                    return locked || grey;
                }
            }
        }

        private const string G = "01 - General", T = "02 - Threat", AB = "03 - Abilities", P = "04 - Packs", TI = "05 - Tiers",
            AF = "06 - Affixes", CH = "07 - Champions", CS = "08 - Creature Setup", V = "09 - Veteran Abilities", B = "10 - Bosses",
            BS = "11 - Boss Scaling", MG = "12 - Monster Gear", SD = "13 - Set Drops";

        private static Rule S(string name, string desc, string cat = null, int? order = null) => new Rule(Show.Shown, cat, name, desc, order);
        private static Rule A(string name, string desc, string cat = null) => new Rule(Show.Advanced, cat, name, desc);

        private static Dictionary<string, Rule> s_rules;
        private static Dictionary<string, Rule> Rules => s_rules ?? (s_rules = new Dictionary<string, Rule>(StringComparer.Ordinal)
        {
            // ---- 01 - General
            [G + "|Enabled"] = S("Monster overhaul", "Turns the whole overhaul on or off.", null, 100),
            [G + "|Lock Configuration"] = A("Server decides", "The server's settings and creature rules apply on every player."),
            [G + "|Greydwarfs"] = A("Smarter Greydwarfs", "Greydwarfs use the overhaul brain."),
            [G + "|Draugr"] = A("Smarter Draugr", "Draugr use the overhaul brain."),
            [G + "|Brain Radius"] = A("Smart AI range (m)", "Monsters this close to a player use the overhaul brain."),

            // ---- 02 - Threat
            [T + "|Switch Margin Melee"] = A("Target switch margin, melee", "How much more threat a melee attacker needs to pull the monster."),
            [T + "|Switch Margin Ranged"] = A("Target switch margin, ranged", "How much more threat a ranged attacker needs to pull the monster."),
            [T + "|Melee Range"] = A("Melee range (m)", "Distance that counts as melee for target switching."),
            [T + "|Decay Seconds"] = A("Threat memory (s)", "Seconds out of combat before a monster forgets its threat."),
            [T + "|Heal Threat Factor"] = A("Healing threat", "Threat from healing, compared to damage."),

            // ---- 03 - Abilities (+ 09 - Veteran Abilities)
            [AB + "|Shaman Heal Cast Seconds"] = A("Shaman heal cast time (s)", "Wind-up of the Greydwarf Shaman's heal; stagger it to cancel."),
            [AB + "|War Cry Cast Seconds"] = A("War cry cast time (s)", "Wind-up of the Draugr Elite's war cry; stagger it to cancel."),
            [AB + "|War Cry Cooldown"] = A("War cry cooldown (s)", "Seconds between war cries."),
            [AB + "|War Cry Radius"] = A("War cry range (m)", "Draugr this close get the war cry buff."),
            [AB + "|War Cry Damage Bonus"] = A("War cry damage bonus", "Extra damage from the war cry (0.15 = +15%)."),
            [AB + "|War Cry Duration"] = A("War cry length (s)", "Seconds the war cry buff lasts."),
            [V + "|Enabled"] = S("Veteran abilities", "Veterans, Elites and Champions get a second ability.", AB, 100),
            [V + "|Ground Blast Cast Seconds"] = A("Ground Blast cast time (s)", "Wind-up of a Caster's Ground Blast.", AB),
            [V + "|Ground Blast Cooldown"] = A("Ground Blast cooldown (s)", "Seconds between Ground Blasts.", AB),
            [V + "|Ground Blast Radius"] = A("Ground Blast size (m)", "Reach of the Ground Blast ring.", AB),
            [V + "|Ground Blast Damage"] = A("Ground Blast damage", "Damage as a share of the caster's max health.", AB),
            [V + "|Charge Windup Seconds"] = A("Charge wind-up (s)", "Wind-up of a Brute's Charge.", AB),
            [V + "|Charge Cooldown"] = A("Charge cooldown (s)", "Seconds between Charges.", AB),
            [V + "|Charge Speed Bonus"] = A("Charge speed bonus", "Extra run speed while charging (0.8 = +80%).", AB),
            [V + "|Charge Slam Radius"] = A("Charge slam size (m)", "Reach of the Charge slam ring.", AB),
            [V + "|Charge Slam Damage"] = A("Charge slam damage", "Damage as a share of the Brute's max health.", AB),
            [V + "|Sidestep Cooldown"] = A("Sidestep cooldown (s)", "Seconds between a Skirmisher's Sidesteps.", AB),
            [V + "|Sidestep Distance"] = A("Sidestep distance (m)", "How far a Skirmisher darts aside.", AB),

            // ---- 04 - Packs
            [P + "|Enabled"] = S("Monster packs", "Some natural spawns lead a pack of followers.", null, 100),
            [P + "|Meadows"] = A("Packs in Meadows", "Greylings, Boars and Necks come in groups."),
            [P + "|Black Forest"] = A("Packs in Black Forest", "A Greydwarf leads a Shaman, an Elite and more Greydwarfs."),
            [P + "|Followers Min"] = A("Fewest followers", "Fewest followers a pack leader brings."),
            [P + "|Followers Max"] = A("Most followers", "Most followers a pack leader brings."),
            [P + "|Max Packs Near Player"] = A("Packs near a player", "No new pack forms while this many are within 60 m of a player."),

            // ---- 05 - Tiers (+ 07 - Champions)
            [TI + "|Enabled"] = S("Monster tiers", "Monsters can be Veteran, Elite or Champion.", null, 100),
            [TI + "|Veteran Chance"] = A("Veteran chance", "Chance a monster is a Veteran."),
            [TI + "|Elite Chance"] = A("Elite chance", "Chance a monster is an Elite."),
            [TI + "|Champion Chance"] = A("Champion chance", "Chance a monster is a Champion."),
            [TI + "|Distance Bonus Per 1000 m"] = A("Distance bonus per 1000 m", "Tier chances grow this much per 1000 m from the world centre."),
            [TI + "|Stars Raise Tier"] = A("Stars raise the tier", "1-star monsters are at least Veteran, 2-star at least Elite."),
            [TI + "|Affixes In Meadows"] = A("Affixes in Meadows", "Off: Meadows monsters stop at Veteran."),
            [CH + "|Half Health Phase"] = A("Champion rally", "At half health a Champion rallies and gains one more affix.", TI),
            [CH + "|Rally Cast Seconds"] = A("Champion rally length (s)", "How long the Champion's rally lasts.", TI),

            // ---- 06 - Affixes
            [AF + "|Mending Aura"] = A("Mending Aura", "Heals hurt allies nearby; interrupt it."),
            [AF + "|Mending Aura Cast Seconds"] = A("Mending Aura cast time (s)", "Wind-up of each heal pulse."),
            [AF + "|Mending Aura Cooldown"] = A("Mending Aura cooldown (s)", "Seconds between heal pulses."),
            [AF + "|Mending Aura Radius"] = A("Mending Aura range (m)", "Allies this close are healed."),
            [AF + "|Mending Aura Heal"] = A("Mending Aura heal", "Heal per pulse, as a share of the ally's max health."),
            [AF + "|Stoneskin"] = A("Stoneskin", "Takes much less damage until staggered."),
            [AF + "|Stoneskin Damage Reduction"] = A("Stoneskin damage cut", "Damage taken off each hit (0.8 = 80% less)."),
            [AF + "|Stoneskin Cracked Seconds"] = A("Stoneskin cracked time (s)", "Seconds a stagger cracks the stone."),
            [AF + "|Enrage"] = A("Enrage", "Hits harder after a while in combat."),
            [AF + "|Enrage After Seconds"] = A("Enrage after (s)", "Seconds in combat before it enrages."),
            [AF + "|Enrage Damage Bonus"] = A("Enrage damage bonus", "Extra damage while enraged (0.5 = +50%)."),
            [AF + "|Enrage Calm Seconds"] = A("Enrage calm-down (s)", "Seconds out of combat before it calms down."),
            [AF + "|Pack Leader"] = A("Pack Leader", "Allies near it hit harder."),
            [AF + "|Pack Leader Radius"] = A("Pack Leader range (m)", "Allies this close are inspired."),
            [AF + "|Pack Leader Damage Bonus"] = A("Pack Leader damage bonus", "Extra damage for inspired allies (0.2 = +20%)."),
            [AF + "|Death Burst"] = A("Death Burst", "Explodes after death; step out of the red ring."),
            [AF + "|Death Burst Radius"] = A("Death Burst size (m)", "Reach of the explosion."),
            [AF + "|Death Burst Delay"] = A("Death Burst delay (s)", "Seconds between death and explosion."),
            [AF + "|Death Burst Damage"] = A("Death Burst damage", "Damage as a share of the monster's max health."),
            [AF + "|Vampiric"] = A("Vampiric", "Heals from the damage it deals."),
            [AF + "|Vampiric Heal"] = A("Vampiric heal", "Share of damage dealt it heals back."),
            [AF + "|Shielded"] = A("Shielded", "A shield soaks damage before its health."),
            [AF + "|Shield Health"] = A("Shield strength", "Shield size, as a share of the monster's max health."),
            [AF + "|Shield Recharge Seconds"] = A("Shield recharge (s)", "Seconds without damage before the shield is full again."),
            [AF + "|Reflective"] = A("Reflective", "While it glows, hits are partly reflected back."),
            [AF + "|Reflect Share"] = A("Reflect share", "Share of each hit sent back to the attacker."),
            [AF + "|Reflect Seconds"] = A("Reflect glow length (s)", "How long each glow lasts."),
            [AF + "|Reflect Cooldown"] = A("Reflect cooldown (s)", "Seconds between glows."),
            [AF + "|Frenzied"] = A("Frenzied", "Gets faster as allies die nearby."),
            [AF + "|Frenzy Speed Per Ally"] = A("Frenzy speed per ally", "Extra speed per ally that died (0.15 = +15%)."),
            [AF + "|Frenzy Radius"] = A("Frenzy range (m)", "Allies dying this close feed the frenzy."),
            [AF + "|Frenzy Max Stacks"] = A("Frenzy max stacks", "Most ally deaths a frenzy counts."),
            [AF + "|Splitting"] = A("Splitting", "Splits in two at half health."),
            [AF + "|Blink"] = A("Blink", "Teleports behind a backline player."),
            [AF + "|Blink Cast Seconds"] = A("Blink cast time (s)", "Wind-up of a Blink; stagger it to cancel."),
            [AF + "|Blink Cooldown"] = A("Blink cooldown (s)", "Seconds between Blinks."),
            [AF + "|Blink Range"] = A("Blink range (m)", "Players this close can be blinked to."),
            [AF + "|Warded"] = A("Warded", "Immune while its ward totem stands nearby."),
            [AF + "|Ward Range"] = A("Ward range (m)", "Immune while this close to its totem."),
            [AF + "|Ward Health"] = A("Ward totem health", "Totem health, as a share of the monster's max health."),
            [AF + "|Ward Totem Scale"] = A("Ward totem size", "Size of the ward totem model."),
            [AF + "|Summoner"] = A("Summoner", "Calls weaker copies of itself; interrupt it."),
            [AF + "|Summon Cast Seconds"] = A("Summon cast time (s)", "Wind-up of the call; stagger it to cancel."),
            [AF + "|Summon Cooldown"] = A("Summon cooldown (s)", "Seconds between calls."),
            [AF + "|Summon Count"] = A("Adds per summon", "Adds each call brings."),
            [AF + "|Summon Max Alive"] = A("Most adds alive", "No call while this many adds are alive."),
            [AF + "|Hexer"] = A("Hexer", "Curses its target: less damage, slower."),
            [AF + "|Hex Cast Seconds"] = A("Hex cast time (s)", "Wind-up of the curse; stagger it to cancel."),
            [AF + "|Hex Cooldown"] = A("Hex cooldown (s)", "Seconds between curses."),
            [AF + "|Hex Seconds"] = A("Hex length (s)", "How long the curse lasts."),
            [AF + "|Hex Damage Penalty"] = A("Hex damage penalty", "Less damage dealt while cursed (0.3 = -30%)."),
            [AF + "|Hex Slow"] = A("Hex slow", "Slower movement while cursed (0.2 = -20%)."),
            [AF + "|Elemental"] = A("Elemental", "Leaves burning, freezing or poison rings."),
            [AF + "|Hazard Interval"] = A("Hazard ring interval (s)", "Seconds between hazard rings."),
            [AF + "|Hazard Seconds"] = A("Hazard ring length (s)", "How long each ring lasts."),
            [AF + "|Hazard Radius"] = A("Hazard ring size (m)", "Reach of each ring."),
            [AF + "|Hazard Damage"] = A("Hazard damage", "Damage per second, as a share of the monster's max health."),
            [AF + "|Spirit Linked"] = A("Spirit-linked", "Comes with a twin that shares damage and revives it."),
            [AF + "|Spirit Link Share"] = A("Spirit link share", "Share of each hit that goes to the twin."),
            [AF + "|Spirit Link Revive Seconds"] = A("Spirit link revive (s)", "Kill both twins within this many seconds."),

            // ---- 08 - Creature Setup
            [CS + "|Enabled"] = S("Creature setup rules", "Applies the creature rules file when a monster appears.", null, 100),
            [CS + "|Boss Level Player Radius"] = A("Boss level range (m)", "Players this close set a new monster's boss level."),

            // ---- 10 - Bosses (+ 11 - Boss Scaling)
            [B + "|Enabled"] = S("Boss overhaul", "Bosses get phases, new attacks and a soft enrage.", null, 100),
            [B + "|Enrage Seconds"] = A("Boss enrage after (s)", "Seconds in combat before a boss enrages."),
            [B + "|Enrage Damage Bonus"] = A("Boss enrage damage bonus", "Extra damage once enraged (0.5 = +50%)."),
            [B + "|Interrupt Share"] = A("Boss interrupt threshold", "Heavy-hit damage needed to break a boss cast, as a share of its health."),
            [B + "|Eikthyr Thunderhead Cooldown"] = A("Eikthyr: Thunderhead cooldown (s)", "Seconds between Thunderheads."),
            [B + "|Eikthyr Thunderhead Cast Seconds"] = A("Eikthyr: Thunderhead cast time (s)", "Wind-up of Thunderhead; stagger it to cancel."),
            [B + "|Eikthyr Thunderhead Radius"] = A("Eikthyr: Thunderhead size (m)", "Reach of the Thunderhead ring."),
            [B + "|Eikthyr Thunderhead Damage"] = A("Eikthyr: Thunderhead damage", "Damage as a share of each player's max health."),
            [B + "|Eikthyr Charge Lane Cooldown"] = A("Eikthyr: charge lane cooldown (s)", "Seconds between charge lanes."),
            [B + "|Eikthyr Charge Lane Windup Seconds"] = A("Eikthyr: charge lane warning (s)", "Seconds the lane shows before the charge."),
            [B + "|Eikthyr Charge Lane Width"] = A("Eikthyr: charge lane width (m)", "Width of the charge lane."),
            [B + "|Eikthyr Charge Lane Damage"] = A("Eikthyr: charge lane damage", "Damage as a share of each player's max health."),
            [B + "|Eikthyr Spirit Stags"] = A("Eikthyr: spirit stags", "Stags called at half health, plus one per extra player."),
            [B + "|Eikthyr Spirit Stags Max"] = A("Eikthyr: most spirit stags", "Most stags called, however many players."),
            [B + "|Spirit Health"] = A("Boss spirit health", "Spirit health, as a share of its boss's."),
            [B + "|Spirit Scale"] = A("Boss spirit size", "Spirit size compared to its boss."),
            [B + "|Elder Entangle Cooldown"] = A("Elder: Entangle cooldown (s)", "Seconds between Entangles."),
            [B + "|Elder Entangle Windup Seconds"] = A("Elder: Entangle warning (s)", "Seconds the ring shows before the roots come."),
            [B + "|Elder Entangle Radius"] = A("Elder: Entangle size (m)", "Reach of the Entangle ring."),
            [B + "|Elder Entangle Seconds"] = A("Elder: longest root (s)", "Longest a player stays rooted."),
            [B + "|Elder Entangle Roots Health"] = A("Elder: root health", "Health of the roots holding a player."),
            [B + "|Bonemass Feeding Add Prefab"] = A("Bonemass: feeding creature", "Creature that crawls to Bonemass to heal him."),
            [B + "|Bonemass Plague Stacks"] = A("Bonemass: plague stacks", "Stacks before the Plague bursts."),
            [B + "|Bonemass Plague Seconds"] = A("Bonemass: plague length (s)", "Seconds the Plague lasts after his last hit."),
            [B + "|Bonemass Plague Burst Radius"] = A("Bonemass: plague burst size (m)", "Reach of the Plague burst."),
            [B + "|Bonemass Plague Burst Delay"] = A("Bonemass: plague burst warning (s)", "Seconds the ring shows before it bursts."),
            [B + "|Bonemass Plague Burst Share"] = A("Bonemass: plague burst damage", "Damage as a share of each player's max health."),
            [B + "|Tethered Sources"] = A("Tethered: sources", "Saplings raised by a Tethered phase."),
            [B + "|Tethered Radius"] = A("Tethered: distance (m)", "How far from the boss the sources stand."),
            [B + "|Tethered Source Health"] = A("Tethered: source health", "Source health, as a share of the boss's max health."),
            [B + "|Tethered Heal Per Second"] = A("Tethered: boss heal per second", "Boss heal while a source stands, as a share of its health."),
            [B + "|Heartwood Scale"] = A("Heartwood size", "Size of the Heartwood sapling model."),
            [B + "|Channelled Phase Seconds"] = A("Channelled: phase length (s)", "How long a Channelled phase lasts."),
            [B + "|Channelled Every"] = A("Channelled: heal interval (s)", "Seconds between channelled heals."),
            [B + "|Channelled Cast Seconds"] = A("Channelled: cast time (s)", "Wind-up of a channelled heal; interrupt it."),
            [B + "|Channelled Heal"] = A("Channelled: heal", "Heal as a share of the boss's max health."),
            [B + "|Shelled Shield"] = A("Shelled: shield", "Shell size, as a share of the boss's max health."),
            [B + "|Shelled Seconds"] = A("Shelled: time to break (s)", "Seconds to break the shell before the boss heals."),
            [B + "|Shelled Heal"] = A("Shelled: heal", "Heal if the shell holds, as a share of max health."),
            [B + "|Feeding Adds"] = A("Feeding: adds", "Adds sent to the boss, plus one per extra player."),
            [B + "|Feeding Add Prefab"] = A("Feeding: creature", "Creature sent when the boss names none."),
            [B + "|Feeding Distance"] = A("Feeding: start distance (m)", "How far from the boss the adds start."),
            [B + "|Feeding Heal"] = A("Feeding: heal per add", "Heal per add that arrives, as a share of max health."),
            // 0.0.40 (Fire: phases for Moder, Yagluth, The Queen and Fader)
            [B + "|Moder Phases"] = A("Moder: phases", "Drakes at the first threshold, frost lanes from the second."),
            [B + "|Moder First Phase Health"] = A("Moder: first phase at", "Health share at which her drakes come."),
            [B + "|Moder Second Phase Health"] = A("Moder: second phase at", "Health share at which her frost lanes start."),
            [B + "|Moder Drakes"] = A("Moder: drakes", "Drakes called, plus one per extra player."),
            [B + "|Moder Drakes Max"] = A("Moder: most drakes", "Most drakes called, however many players."),
            [B + "|Moder Drake Prefab"] = A("Moder: drake creature", "Creature Moder calls."),
            [B + "|Moder Frost Lane Cooldown"] = A("Moder: frost lane cooldown (s)", "Seconds between frost lanes."),
            [B + "|Moder Frost Lane Damage Per Second"] = A("Moder: frost lane damage per second", "Frost damage per second on the lane's ground."),
            [B + "|Yagluth Phases"] = A("Yagluth: phases", "Fire ring at the first threshold, Fulings at the second."),
            [B + "|Yagluth First Phase Health"] = A("Yagluth: first phase at", "Health share at which the fire ring bursts."),
            [B + "|Yagluth Second Phase Health"] = A("Yagluth: second phase at", "Health share at which his Fulings come."),
            [B + "|Yagluth Fire Ring Radius"] = A("Yagluth: fire ring size (m)", "Reach of the fire ring."),
            [B + "|Yagluth Fire Ring Damage Share"] = A("Yagluth: fire ring damage", "Damage as a share of each player's max health."),
            [B + "|Yagluth Fulings"] = A("Yagluth: Fulings", "Fulings called, plus one per extra player."),
            [B + "|Yagluth Fulings Max"] = A("Yagluth: most Fulings", "Most Fulings called, however many players."),
            [B + "|Yagluth Fuling Prefab"] = A("Yagluth: Fuling creature", "Creature Yagluth calls."),
            [B + "|Queen Phases"] = A("Queen: phases", "Seekers at the first threshold, Entangle from the second."),
            [B + "|Queen First Phase Health"] = A("Queen: first phase at", "Health share at which her Seekers come."),
            [B + "|Queen Second Phase Health"] = A("Queen: second phase at", "Health share at which she starts Entangling."),
            [B + "|Queen Seekers"] = A("Queen: Seekers", "Seekers called, plus one per extra player."),
            [B + "|Queen Seekers Max"] = A("Queen: most Seekers", "Most Seekers called, however many players."),
            [B + "|Queen Seeker Prefab"] = A("Queen: Seeker creature", "Creature the Queen calls."),
            [B + "|Fader Phases"] = A("Fader: phases", "Burning field at the first threshold, Charred at the second."),
            [B + "|Fader First Phase Health"] = A("Fader: first phase at", "Health share at which his burning field spreads."),
            [B + "|Fader Second Phase Health"] = A("Fader: second phase at", "Health share at which his Charred come."),
            [B + "|Fader Hazard Rings"] = A("Fader: burning rings", "Rings in his burning field."),
            [B + "|Fader Hazard Damage Per Second"] = A("Fader: burning damage per second", "Fire damage per second inside a ring."),
            [B + "|Fader Charred"] = A("Fader: Charred", "Charred called, plus one per extra player."),
            [B + "|Fader Charred Max"] = A("Fader: most Charred", "Most Charred called, however many players."),
            [B + "|Fader Charred Prefab"] = A("Fader: Charred creature", "Creature Fader calls."),
            [BS + "|Enabled"] = A("Boss group scaling", "Bosses and mini bosses scale with the engaged players. Admin, server only.", B),
            [BS + "|Replace Vanilla Group Scaling"] = A("Replace vanilla group scaling", "This scaling replaces vanilla's for bosses. Admin, server only.", B),
            [BS + "|Mini Bosses Include Elites"] = A("Scale Elites as mini bosses", "Elites scale as mini bosses. Admin, server only.", B),
            [BS + "|Mini Bosses Include Champions"] = A("Scale Champions as mini bosses", "Champions scale as mini bosses. Admin, server only.", B),
            [BS + "|Mini Bosses Include Hildir Bosses"] = A("Scale Hildir bosses as mini bosses", "Hildir's quest bosses scale as mini bosses. Admin, server only.", B),
            [BS + "|Engage Radius"] = A("Scaling: engage range (m)", "Engaged players this close to the boss count. Admin, server only.", B),
            [BS + "|Engage Memory"] = A("Scaling: engage memory (s)", "A player who hit the boss counts this long after. Admin, server only.", B),
            [BS + "|Recount Seconds"] = A("Scaling: recount every (s)", "How often the group is recounted. Admin, server only.", B),
            [BS + "|Shrink Grace"] = A("Scaling: shrink delay (s)", "Seconds the highest count holds. Admin, server only.", B),
            [BS + "|Max Players"] = A("Scaling: most players", "Most players the scaling counts. Admin, server only.", B),
            [BS + "|HP Per Player"] = A("Scaling: health per player", "Extra health per extra player (0.6 = +60%). Admin, server only.", B),
            [BS + "|Resist Per Player"] = A("Scaling: defence per player", "Extra defence per extra player. Admin, server only.", B),
            [BS + "|Damage Per Player"] = A("Scaling: damage per player", "Extra damage per extra player (0.15 = +15%). Admin, server only.", B),
            [BS + "|Resist Floor"] = A("Scaling: least damage taken", "Least share of a hit a scaled boss takes. Admin, server only.", B),
            [BS + "|Companion Weight"] = A("Scaling: companion weight", "A companion counts as this much of a player. Admin, server only.", B),
            [BS + "|Power Strength"] = A("Scaling: power strength", "How strongly group power moves the boss; 0 = off. Admin, server only.", B),
            [BS + "|Power Min"] = A("Scaling: lowest power factor", "Lowest power factor (under-geared group). Admin, server only.", B),
            [BS + "|Power Max"] = A("Scaling: highest power factor", "Highest power factor (over-geared group). Admin, server only.", B),
            [BS + "|Power Damage Share"] = A("Scaling: power to damage", "Share of the power factor that also raises damage. Admin, server only.", B),
            [BS + "|Power Weight Gear"] = A("Power weight: gear", "Weight of armour and weapon in the power score. Admin, server only.", B),
            [BS + "|Power Weight Quality"] = A("Power weight: upgrades", "Weight of gear upgrade level. Admin, server only.", B),
            [BS + "|Power Weight Crafting"] = A("Power weight: loot rarity", "Weight of Fires Loot Overhaul rarity, affixes and gems. Admin, server only.", B),
            [BS + "|Power Weight Classes"] = A("Power weight: class level", "Weight of RPGClasses level. Admin, server only.", B),
            [BS + "|Power Weight Skills"] = A("Power weight: skills", "Weight of weapon and Blocking skill. Admin, server only.", B),
            [BS + "|Expected Power Per Biome"] = A("Expected power per biome", "Biome=armour:weapon:skill a group is expected to bring. Admin, server only.", B),

            // ---- 12 - Monster Gear
            [MG + "|Enabled"] = S("Monster gear", "Draugr and Skeletons can wear real armour that may drop.", null, 100),
            [MG + "|Prefabs"] = A("Monsters that wear gear", "Monster prefab names, comma-separated."),
            [MG + "|Chance"] = A("Gear chance", "Chance a monster rolls gear."),
            [MG + "|Pieces By Tier"] = A("Pieces by tier", "Min-max pieces for Normal, Veteran, Elite, Champion."),
            [MG + "|Gear Tables"] = A("Gear per biome", "Biome=item,item; the armour each biome draws from."),
            [MG + "|Loot Luck By Tier"] = A("Loot luck by tier", "Fires Loot Overhaul luck for Normal, Veteran, Elite, Champion."),
            [MG + "|Drop Chance"] = A("Gear drop chance", "Chance each worn piece drops on death."),
            [MG + "|Champion Drop Chance"] = A("Champion gear drop chance", "Drop chance for Champions."),
            [MG + "|Drop Durability"] = A("Dropped gear durability", "Share of max durability a dropped piece keeps."),
            [MG + "|Armour Factor"] = A("Gear armour multiplier", "Multiplier on the worn armour's protection."),

            // ---- 13 - Set Drops
            [SD + "|Enabled"] = S("Set drops", "Fires Loot Overhaul set pieces drop from set sources.", null, 100),
            [SD + "|Bad Luck Share"] = A("Bad luck protection", "Each miss raises your next chance by this share of the set's chance."),
            [SD + "|Legendary Chance"] = A("Legendary chance", "Chance a set piece is Legendary instead of Epic."),
            [SD + "|Engage Range"] = A("Engage range (m)", "Players this close on the threat table count for the drop."),
            // 0.0.49: keys moved from Core keep Core's names and texts
            ["MonsterArmorFit|Measured helmet fit"] = new Rule(Show.Advanced),
            ["MonsterArmorFit|Paint armour textures on monsters"] = new Rule(Show.Advanced),
            ["MonsterArmorFit|Vanilla bind on identical rigs"] = new Rule(Show.Advanced),
            ["MonsterArmorFit|Split unmapped neck"] = new Rule(Show.Advanced),
            ["Movement|Monster fall damage"] = new Rule(Show.Shown),
            // ---- Creature Diving (0.0.47, moved from Galaxies; these three never had a rule)
            ["Creature Diving|CreatureDivingEnabled"] = S("Creature diving", "Creatures dive after a diving target. Needs Fires Advanced Player Movement."),
            ["Creature Diving|CreatureMaxDiveDepth"] = A("Creature dive depth (m)", "How far below the surface a creature sinks chasing a goal."),
            ["Creature Diving|CreatureDiveSpeed"] = A("Creature dive speed (m/s)", "How fast a creature descends or ascends following its goal."),
        });

        private static Rule RuleFor(ConfigDefinition d, out bool matched)
        {
            matched = Rules.TryGetValue(d.Section + "|" + d.Key, out var r);
            return matched ? r : new Rule(Show.Advanced);
        }

        private static readonly FieldInfo DescriptionField = typeof(ConfigEntryBase).GetField("<Description>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>Tags every entry of FMO's cfg not tagged yet. Safe to call again (call after any late Bind).</summary>
        internal static void Apply(ConfigFile config)
        {
            if (config == null || DescriptionField == null) return;
            int tagged = 0;
            foreach (var entry in config.Keys.Select(k => config[k]).ToList())
            {
                var old = entry.Description;
                var tags = old?.Tags ?? Array.Empty<object>();
                if (tags.Any(t => t is ConfigurationManagerAttributes)) continue;
                var rule = RuleFor(entry.Definition, out bool matched);
                if (!matched) MonsterLog.Warning($"config ui: no rule for [{entry.Definition.Section}] {entry.Definition.Key}; shown as Advanced");
                var attr = new ConfigurationManagerAttributes
                {
                    Browsable = rule.Show != Show.Hidden,
                    IsAdvanced = rule.Show == Show.Advanced,
                    Order = rule.Order,
                    Category = rule.Category,
                    DispName = rule.Name,
                    Description = rule.Desc,
                    Grey = rule.Grey,
                    Prior = tags.LastOrDefault(t => t != null && t.GetType().Name == nameof(ConfigurationManagerAttributes)),
                };
                var list = tags.ToList(); list.Add(attr);
                DescriptionField.SetValue(entry, new ConfigDescription(rule.Desc ?? old?.Description ?? "", old?.AcceptableValues, list.ToArray()));
                tagged++;
            }
            if (tagged > 0) MonsterLog.Info($"config ui: settings UI tags applied to {tagged} entr{(tagged == 1 ? "y" : "ies")}.");
        }
    }
}
