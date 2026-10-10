using System.Reflection;
using BepInEx.Configuration;
using FiresCore.Sync;

namespace FiresMonsterOverhaul
{
    internal static class MonsterConfig
    {
        public static ConfigEntry<bool> LockConfiguration;
        public static ConfigEntry<bool> CreatureSetupEnabled;
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<bool> Greydwarfs;
        public static ConfigEntry<bool> Draugr;
        public static ConfigEntry<float> BrainRadius;

        public static ConfigEntry<float> ThreatSwitchMelee;
        public static ConfigEntry<float> ThreatSwitchRanged;
        public static ConfigEntry<float> MeleeRange;
        public static ConfigEntry<float> ThreatDecaySeconds;
        public static ConfigEntry<float> HealThreatFactor;

        public static ConfigEntry<float> ShamanHealCastSeconds;
        public static ConfigEntry<float> WarCryCastSeconds;
        public static ConfigEntry<float> WarCryCooldown;
        public static ConfigEntry<float> WarCryRadius;
        public static ConfigEntry<float> WarCryDamageBonus;
        public static ConfigEntry<float> WarCryDuration;

        public static ConfigEntry<bool> PacksEnabled;
        public static ConfigEntry<bool> PacksMeadows;
        public static ConfigEntry<bool> PacksBlackForest;
        public static ConfigEntry<int> PackFollowersMin;
        public static ConfigEntry<int> PackFollowersMax;
        public static ConfigEntry<int> MaxPacksNearPlayer;

        public static ConfigEntry<bool> TiersEnabled;
        public static ConfigEntry<float> VeteranChance;
        public static ConfigEntry<float> EliteChance;
        public static ConfigEntry<float> ChampionChance;
        public static ConfigEntry<float> TierDistanceBonus;
        public static ConfigEntry<bool> StarsRaiseTier;
        public static ConfigEntry<bool> AffixesInMeadows;

        public static ConfigEntry<bool> MendingAura;
        public static ConfigEntry<float> MendingAuraCastSeconds;
        public static ConfigEntry<float> MendingAuraCooldown;
        public static ConfigEntry<float> MendingAuraRadius;
        public static ConfigEntry<float> MendingAuraHeal;
        public static ConfigEntry<bool> Stoneskin;
        public static ConfigEntry<float> StoneskinDamageReduction;
        public static ConfigEntry<float> StoneskinCrackedSeconds;
        public static ConfigEntry<bool> Enrage;
        public static ConfigEntry<float> EnrageAfterSeconds;
        public static ConfigEntry<float> EnrageDamageBonus;
        public static ConfigEntry<float> EnrageCalmSeconds;
        public static ConfigEntry<bool> PackLeader;
        public static ConfigEntry<float> PackLeaderRadius;
        public static ConfigEntry<float> PackLeaderDamageBonus;
        public static ConfigEntry<bool> DeathBurst;
        public static ConfigEntry<float> DeathBurstRadius;
        public static ConfigEntry<float> DeathBurstDelay;
        public static ConfigEntry<float> DeathBurstDamage;
        public static ConfigEntry<bool> ChampionPhase;
        public static ConfigEntry<float> ChampionPhaseCastSeconds;

        public static ConfigEntry<bool> Vampiric;
        public static ConfigEntry<float> VampiricHeal;
        public static ConfigEntry<bool> Shielded;
        public static ConfigEntry<float> ShieldHealth;
        public static ConfigEntry<float> ShieldRechargeSeconds;
        public static ConfigEntry<bool> Reflective;
        public static ConfigEntry<float> ReflectShare;
        public static ConfigEntry<float> ReflectSeconds;
        public static ConfigEntry<float> ReflectCooldown;
        public static ConfigEntry<bool> Frenzied;
        public static ConfigEntry<float> FrenzySpeedPerAlly;
        public static ConfigEntry<float> FrenzyRadius;
        public static ConfigEntry<int> FrenzyMaxStacks;
        public static ConfigEntry<bool> Splitting;
        public static ConfigEntry<bool> Blink;
        public static ConfigEntry<float> BlinkCastSeconds;
        public static ConfigEntry<float> BlinkCooldown;
        public static ConfigEntry<float> BlinkRange;
        public static ConfigEntry<bool> Warded;
        public static ConfigEntry<float> WardRange;
        public static ConfigEntry<float> WardHealth;
        public static ConfigEntry<float> WardTotemScale;
        public static ConfigEntry<bool> Summoner;
        public static ConfigEntry<float> SummonCastSeconds;
        public static ConfigEntry<float> SummonCooldown;
        public static ConfigEntry<int> SummonCount;
        public static ConfigEntry<int> SummonMaxAlive;
        public static ConfigEntry<bool> Hexer;
        public static ConfigEntry<float> HexCastSeconds;
        public static ConfigEntry<float> HexCooldown;
        public static ConfigEntry<float> HexSeconds;
        public static ConfigEntry<float> HexDamagePenalty;
        public static ConfigEntry<float> HexSlow;
        public static ConfigEntry<bool> Elemental;
        public static ConfigEntry<float> HazardInterval;
        public static ConfigEntry<float> HazardSeconds;
        public static ConfigEntry<float> HazardRadius;
        public static ConfigEntry<float> HazardDamage;
        public static ConfigEntry<bool> SpiritLinked;
        public static ConfigEntry<float> SpiritLinkShare;
        public static ConfigEntry<float> SpiritLinkReviveSeconds;

        public static ConfigEntry<bool> Bosses;
        public static ConfigEntry<float> BossEnrageSeconds;
        public static ConfigEntry<float> BossEnrageDamageBonus;
        public static ConfigEntry<float> BossInterruptShare;
        public static ConfigEntry<float> ThunderheadCooldown;
        public static ConfigEntry<float> ThunderheadCastSeconds;
        public static ConfigEntry<float> ThunderheadRadius;
        public static ConfigEntry<float> ThunderheadDamage;
        public static ConfigEntry<float> ChargeLaneCooldown;
        public static ConfigEntry<float> ChargeLaneWindupSeconds;
        public static ConfigEntry<float> ChargeLaneWidth;
        public static ConfigEntry<float> ChargeLaneDamage;
        public static ConfigEntry<int> EikthyrSpiritStags;
        public static ConfigEntry<int> EikthyrSpiritStagsMax;
        public static ConfigEntry<float> SpiritHealth;
        public static ConfigEntry<float> SpiritScale;
        public static ConfigEntry<float> EntangleCooldown;
        public static ConfigEntry<float> EntangleWindupSeconds;
        public static ConfigEntry<float> EntangleRadius;
        public static ConfigEntry<float> EntangleSeconds;
        public static ConfigEntry<float> EntangleRootsHealth;
        public static ConfigEntry<int> TetherCount;
        public static ConfigEntry<float> TetherRadius;
        public static ConfigEntry<float> TetherHealth;
        public static ConfigEntry<float> TetherHealPerSecond;
        public static ConfigEntry<float> HeartwoodScale;
        public static ConfigEntry<float> ChannelPhaseSeconds;
        public static ConfigEntry<float> ChannelEvery;
        public static ConfigEntry<float> ChannelCastSeconds;
        public static ConfigEntry<float> ChannelHeal;
        public static ConfigEntry<float> ShellShare;
        public static ConfigEntry<float> ShellSeconds;
        public static ConfigEntry<float> ShellHeal;
        public static ConfigEntry<int> FeederCount;
        public static ConfigEntry<string> FeederPrefab;
        public static ConfigEntry<float> FeederDistance;
        public static ConfigEntry<float> FeederHeal;

        public static ConfigEntry<bool> VeteranAbilities;
        public static ConfigEntry<float> GroundBlastCastSeconds;
        public static ConfigEntry<float> GroundBlastCooldown;
        public static ConfigEntry<float> GroundBlastRadius;
        public static ConfigEntry<float> GroundBlastDamage;
        public static ConfigEntry<float> ChargeWindupSeconds;
        public static ConfigEntry<float> ChargeCooldown;
        public static ConfigEntry<float> ChargeSpeedBonus;
        public static ConfigEntry<float> ChargeSlamRadius;
        public static ConfigEntry<float> ChargeSlamDamage;
        public static ConfigEntry<float> SidestepCooldown;
        public static ConfigEntry<float> SidestepDistance;

        public static void Bind(ConfigFile cfg)
        {
            const string tiers = "5 - Tiers";
            TiersEnabled = cfg.Bind(tiers, "Enabled", true, "Monsters roll a tier when they appear (doc 21): Normal, Veteran, Elite (one affix) or Champion (two affixes and a name). Bosses and tames never do.");
            VeteranChance = cfg.Bind(tiers, "Veteran Chance", 0.15f, "Chance a monster is a Veteran (before the distance bonus).");
            EliteChance = cfg.Bind(tiers, "Elite Chance", 0.05f, "Chance a monster is an Elite (before the distance bonus).");
            ChampionChance = cfg.Bind(tiers, "Champion Chance", 0.01f, "Chance a monster is a Champion (before the distance bonus).");
            TierDistanceBonus = cfg.Bind(tiers, "Distance Bonus Per 1000 m", 0.1f, "The tier chances grow by this fraction for every 1000 m from the world centre (0.1 = x1.5 at 5000 m).");
            StarsRaiseTier = cfg.Bind(tiers, "Stars Raise Tier", true, "A 1-star monster is at least a Veteran, a 2-star one at least an Elite.");
            AffixesInMeadows = cfg.Bind(tiers, "Affixes In Meadows", false, "When off, Meadows monsters stop at Veteran, so the first biome has no affixes.");

            const string affixes = "6 - Affixes";
            MendingAura = cfg.Bind(affixes, "Mending Aura", true, "Elites and Champions may get Mending Aura: an interruptible pulse that heals hurt allies nearby. Counter: interrupt it, or pull it away.");
            MendingAuraCastSeconds = cfg.Bind(affixes, "Mending Aura Cast Seconds", 1.0f, "Wind-up of each Mending Aura pulse; a stagger or a class stun in this window cancels it.");
            MendingAuraCooldown = cfg.Bind(affixes, "Mending Aura Cooldown", 3f, "Seconds between Mending Aura pulses.");
            MendingAuraRadius = cfg.Bind(affixes, "Mending Aura Radius", 10f, "Metres within which a Mending Aura pulse heals allies.");
            MendingAuraHeal = cfg.Bind(affixes, "Mending Aura Heal", 0.08f, "Health each pulse gives every hurt ally, as a fraction of the ally's max health.");
            Stoneskin = cfg.Bind(affixes, "Stoneskin", true, "Elites and Champions may get Stoneskin: much less damage taken until staggered. Counter: stagger it (parries, heavy hits, class stuns).");
            StoneskinDamageReduction = cfg.Bind(affixes, "Stoneskin Damage Reduction", 0.8f, "Damage Stoneskin takes off each hit (0.8 = 80 % less). The hit's full stagger still counts.");
            StoneskinCrackedSeconds = cfg.Bind(affixes, "Stoneskin Cracked Seconds", 10f, "Seconds a stagger cracks Stoneskin for (full damage), before it hardens again.");
            Enrage = cfg.Bind(affixes, "Enrage", true, "Elites and Champions may get Enrage: more damage after a while in combat. Counter: kill it in time.");
            EnrageAfterSeconds = cfg.Bind(affixes, "Enrage After Seconds", 45f, "Seconds in combat before an Enrage monster enrages.");
            EnrageDamageBonus = cfg.Bind(affixes, "Enrage Damage Bonus", 0.5f, "Extra damage while enraged (0.5 = +50%).");
            EnrageCalmSeconds = cfg.Bind(affixes, "Enrage Calm Seconds", 10f, "Seconds out of combat after which an enraged monster calms down and its clock restarts.");
            PackLeader = cfg.Bind(affixes, "Pack Leader", true, "Elites and Champions may get Pack Leader: while it fights, allies near it hit harder. Counter: kill it first, or taunt it away from its pack.");
            PackLeaderRadius = cfg.Bind(affixes, "Pack Leader Radius", 15f, "Metres within which a Pack Leader inspires its allies.");
            PackLeaderDamageBonus = cfg.Bind(affixes, "Pack Leader Damage Bonus", 0.2f, "Extra damage for inspired allies (0.2 = +20%).");
            DeathBurst = cfg.Bind(affixes, "Death Burst", true, "Elites and Champions may get Death Burst: on death a red ring marks where it will explode. Counter: step out of the ring.");
            DeathBurstRadius = cfg.Bind(affixes, "Death Burst Radius", 4f, "Metres the Death Burst explosion reaches.");
            DeathBurstDelay = cfg.Bind(affixes, "Death Burst Delay", 2f, "Seconds between the death and the explosion (the ring shows for this long).");
            DeathBurstDamage = cfg.Bind(affixes, "Death Burst Damage", 0.2f, "Explosion damage to each player inside the ring, as a fraction of the monster's max health.");
            Vampiric = cfg.Bind(affixes, "Vampiric", true, "Elites and Champions may get Vampiric: they heal from the damage they deal. Counter: anti-heal, or kite it.");
            VampiricHeal = cfg.Bind(affixes, "Vampiric Heal", 0.5f, "Health a Vampiric monster gets back, as a fraction of the damage it deals.");
            Shielded = cfg.Bind(affixes, "Shielded", true, "Elites and Champions may get Shielded: an absorb shield takes damage before its health does. Counter: burst it down.");
            ShieldHealth = cfg.Bind(affixes, "Shield Health", 0.4f, "The shield's strength, as a fraction of the monster's max health.");
            ShieldRechargeSeconds = cfg.Bind(affixes, "Shield Recharge Seconds", 15f, "Seconds without taking damage after which a broken or worn shield is full again.");
            Reflective = cfg.Bind(affixes, "Reflective", true, "Elites and Champions may get Reflective: now and then it glows for a few seconds and hits taken during the glow are partly reflected back at the attacker. Counter: stop hitting while it glows.");
            ReflectShare = cfg.Bind(affixes, "Reflect Share", 0.5f, "Share of each hit taken during the glow that is dealt back to the attacker (it still takes the hit).");
            ReflectSeconds = cfg.Bind(affixes, "Reflect Seconds", 3f, "How long each glow lasts.");
            ReflectCooldown = cfg.Bind(affixes, "Reflect Cooldown", 12f, "Seconds between glows (counted from the start of one).");
            Frenzied = cfg.Bind(affixes, "Frenzied", true, "Elites and Champions may get Frenzied: faster with each ally that dies nearby. Counter: kill it first, or crowd-control it.");
            FrenzySpeedPerAlly = cfg.Bind(affixes, "Frenzy Speed Per Ally", 0.15f, "Extra move speed for each ally that died nearby (0.15 = +15%).");
            FrenzyRadius = cfg.Bind(affixes, "Frenzy Radius", 15f, "Metres within which an ally's death feeds the frenzy.");
            FrenzyMaxStacks = cfg.Bind(affixes, "Frenzy Max Stacks", 5, "Most allies' deaths a frenzy counts.");
            Splitting = cfg.Bind(affixes, "Splitting", true, "Elites and Champions may get Splitting: at half health it splits in two (the copy has the same health, no tier, no affixes and drops nothing). Counter: area damage.");
            Blink = cfg.Bind(affixes, "Blink", true, "Elites and Champions may get Blink: after a short cast it teleports behind the farthest player it can see who is not its current target (the backline). Counter: interrupt the cast, taunt, crowd control to peel it.");
            BlinkCastSeconds = cfg.Bind(affixes, "Blink Cast Seconds", 0.8f, "Wind-up of a Blink; a stagger or a class stun cancels it.");
            BlinkCooldown = cfg.Bind(affixes, "Blink Cooldown", 10f, "Seconds between Blinks.");
            BlinkRange = cfg.Bind(affixes, "Blink Range", 25f, "Metres within which a Blink picks its player.");
            Warded = cfg.Bind(affixes, "Warded", true, "Elites and Champions may get Warded: when it first fights it raises a ward totem nearby and takes no damage while the totem stands within range. Counter: break the totem (switch targets, area damage).");
            WardRange = cfg.Bind(affixes, "Ward Range", 20f, "Metres from its totem within which a Warded monster is immune.");
            WardHealth = cfg.Bind(affixes, "Ward Health", 0.25f, "The ward totem's health, as a fraction of the monster's max health (at least 50).");
            WardTotemScale = cfg.Bind(affixes, "Ward Totem Scale", 3f, "Size of the ward totem (the vanilla Fuling totem's model, scaled up).");
            Summoner = cfg.Bind(affixes, "Summoner", true, "Elites and Champions may get Summoner: an interruptible call that brings weaker copies of itself (half health, no tier, no drops). Counter: interrupt the call, area damage on the adds.");
            SummonCastSeconds = cfg.Bind(affixes, "Summon Cast Seconds", 2f, "Wind-up of a Summoner's call; a stagger or a class stun cancels it.");
            SummonCooldown = cfg.Bind(affixes, "Summon Cooldown", 20f, "Seconds between a Summoner's calls.");
            SummonCount = cfg.Bind(affixes, "Summon Count", 2, "Adds each call brings.");
            SummonMaxAlive = cfg.Bind(affixes, "Summon Max Alive", 4, "Most of a Summoner's adds alive at once (no call while it has this many).");
            Hexer = cfg.Bind(affixes, "Hexer", true, "Elites and Champions may get Hexer: an interruptible cast that curses its target (less damage dealt, slower). Counter: interrupt it, or cleanse the curse.");
            HexCastSeconds = cfg.Bind(affixes, "Hex Cast Seconds", 1.2f, "Wind-up of a Hexer's curse; a stagger or a class stun cancels it.");
            HexCooldown = cfg.Bind(affixes, "Hex Cooldown", 15f, "Seconds between a Hexer's curses.");
            HexSeconds = cfg.Bind(affixes, "Hex Seconds", 8f, "How long the curse lasts.");
            HexDamagePenalty = cfg.Bind(affixes, "Hex Damage Penalty", 0.3f, "Less damage dealt while cursed (0.3 = -30%).");
            HexSlow = cfg.Bind(affixes, "Hex Slow", 0.2f, "Slower movement while cursed (0.2 = -20%).");
            Elemental = cfg.Bind(affixes, "Elemental", true, "Elites and Champions may get Molten / Frostbound / Plagued (the element follows its biome): while it fights it leaves rings of burning, freezing or poisoned ground. Counter: move out; cleanse the effect.");
            HazardInterval = cfg.Bind(affixes, "Hazard Interval", 3f, "Seconds between the hazard rings an Elemental monster leaves.");
            HazardSeconds = cfg.Bind(affixes, "Hazard Seconds", 8f, "How long each hazard ring lasts.");
            HazardRadius = cfg.Bind(affixes, "Hazard Radius", 2.5f, "Metres each hazard ring reaches.");
            HazardDamage = cfg.Bind(affixes, "Hazard Damage", 0.02f, "Elemental damage per second to a player standing in a ring, as a fraction of the monster's max health (fire burns, frost chills, poison poisons: vanilla's own effects).");
            SpiritLinked = cfg.Bind(affixes, "Spirit Linked", true, "Elites and Champions may get Spirit-linked: it comes with a twin; the two share damage, and the survivor brings the other back unless both die within a few seconds of each other. Counter: burst them down together.");
            SpiritLinkShare = cfg.Bind(affixes, "Spirit Link Share", 0.5f, "Share of each hit that goes to the twin instead.");
            SpiritLinkReviveSeconds = cfg.Bind(affixes, "Spirit Link Revive Seconds", 10f, "Seconds after one twin dies before the survivor brings it back (both must die within this window).");

            const string champions = "7 - Champions";
            ChampionPhase = cfg.Bind(champions, "Half Health Phase", true, "At half health a Champion stops for an uninterruptible rally (a burst window) and gains one more affix (doc 21).");
            ChampionPhaseCastSeconds = cfg.Bind(champions, "Rally Cast Seconds", 1.5f, "Length of the Champion's half-health rally; it stands still and can't be interrupted.");

            const string bosses = "10 - Bosses";
            Bosses = cfg.Bind(bosses, "Enabled", true, "Vanilla bosses get phases, new attacks, healing phases and a soft enrage (doc 22). Eikthyr: Thunderhead, a charge lane, spirit stags. The Elder: Entangle, Tethered Heartwood.");
            BossEnrageSeconds = cfg.Bind(bosses, "Enrage Seconds", 480f, "Seconds in combat after which a boss enrages (soft enrage).");
            BossEnrageDamageBonus = cfg.Bind(bosses, "Enrage Damage Bonus", 0.5f, "Extra damage once a boss has enraged (0.5 = +50%).");
            BossInterruptShare = cfg.Bind(bosses, "Interrupt Share", 0.06f, "Bosses can't be staggered, so their interruptible casts (Thunderhead, channelled heals) break on heavy hits instead: the hits' stagger damage during one cast, as a share of the boss's max health. Blunt and heavy (secondary) attacks count most.");
            ThunderheadCooldown = cfg.Bind(bosses, "Eikthyr Thunderhead Cooldown", 25f, "Seconds between Eikthyr's Thunderheads.");
            ThunderheadCastSeconds = cfg.Bind(bosses, "Eikthyr Thunderhead Cast Seconds", 3f, "Wind-up of Thunderhead; a stagger (a parry, a heavy hit, Shield Bash) or a class stun in this window cancels it. The teaching fight's lesson.");
            ThunderheadRadius = cfg.Bind(bosses, "Eikthyr Thunderhead Radius", 6f, "Metres the Thunderhead ring reaches around Eikthyr.");
            ThunderheadDamage = cfg.Bind(bosses, "Eikthyr Thunderhead Damage", 0.25f, "Thunderhead's damage to each player in the ring, as a share of that player's max health.");
            ChargeLaneCooldown = cfg.Bind(bosses, "Eikthyr Charge Lane Cooldown", 18f, "Seconds between Eikthyr's charge lanes.");
            ChargeLaneWindupSeconds = cfg.Bind(bosses, "Eikthyr Charge Lane Windup Seconds", 1.2f, "Seconds the lane shows before Eikthyr charges down it.");
            ChargeLaneWidth = cfg.Bind(bosses, "Eikthyr Charge Lane Width", 2f, "Width of the charge lane in metres.");
            ChargeLaneDamage = cfg.Bind(bosses, "Eikthyr Charge Lane Damage", 0.2f, "Damage to each player still in the lane when the charge ends, as a share of that player's max health.");
            EikthyrSpiritStags = cfg.Bind(bosses, "Eikthyr Spirit Stags", 2, "Spirit stags Eikthyr calls at half health (small crystal copies of him), plus one per extra player within 40 m.");
            EikthyrSpiritStagsMax = cfg.Bind(bosses, "Eikthyr Spirit Stags Max", 4, "Most spirit stags Eikthyr calls, however many players.");
            SpiritHealth = cfg.Bind(bosses, "Spirit Health", 0.1f, "A boss spirit's health, as a share of its boss's.");
            SpiritScale = cfg.Bind(bosses, "Spirit Scale", 0.4f, "A boss spirit's size relative to its boss.");
            EntangleCooldown = cfg.Bind(bosses, "Elder Entangle Cooldown", 20f, "Seconds between the Elder's Entangles.");
            EntangleWindupSeconds = cfg.Bind(bosses, "Elder Entangle Windup Seconds", 1.2f, "Seconds the ring shows at a player's feet before the roots come (it can't be interrupted). Counter: step out of the ring.");
            EntangleRadius = cfg.Bind(bosses, "Elder Entangle Radius", 2.5f, "Metres the Entangle ring reaches.");
            EntangleSeconds = cfg.Bind(bosses, "Elder Entangle Seconds", 12f, "Longest a player stays rooted if nobody breaks the roots. Purify cleanses it.");
            EntangleRootsHealth = cfg.Bind(bosses, "Elder Entangle Roots Health", 60f, "Health of the roots that hold a player (anyone can break them, the rooted player too).");
            TetherCount = cfg.Bind(bosses, "Tethered Sources", 3, "Heartwood saplings (or a later boss's sources) raised by a Tethered phase. The Elder's comes at half health.");
            TetherRadius = cfg.Bind(bosses, "Tethered Radius", 12f, "Metres from the boss the Tethered sources stand.");
            TetherHealth = cfg.Bind(bosses, "Tethered Source Health", 0.04f, "Each Tethered source's health, as a share of the boss's max health (at least 50).");
            TetherHealPerSecond = cfg.Bind(bosses, "Tethered Heal Per Second", 0.01f, "The boss's heal per second while any Tethered source stands, as a share of its max health. It's immune meanwhile.");
            HeartwoodScale = cfg.Bind(bosses, "Heartwood Scale", 4f, "Size of the Heartwood sapling (the vanilla beech sapling's model, scaled up).");
            ChannelPhaseSeconds = cfg.Bind(bosses, "Channelled Phase Seconds", 40f, "How long a Channelled phase lasts.");
            ChannelEvery = cfg.Bind(bosses, "Channelled Every", 12f, "Seconds between a Channelled phase's heal casts.");
            ChannelCastSeconds = cfg.Bind(bosses, "Channelled Cast Seconds", 5f, "Wind-up of a channelled heal; a stagger or a class stun cancels it. Counter: interrupt every cast.");
            ChannelHeal = cfg.Bind(bosses, "Channelled Heal", 0.1f, "What a finished channelled heal restores, as a share of the boss's max health.");
            ShellShare = cfg.Bind(bosses, "Shelled Shield", 0.15f, "A Shelled phase's absorb shield, as a share of the boss's max health.");
            ShellSeconds = cfg.Bind(bosses, "Shelled Seconds", 15f, "Seconds to break the shell before the boss heals.");
            ShellHeal = cfg.Bind(bosses, "Shelled Heal", 0.15f, "What the boss heals if its shell holds, as a share of its max health.");
            FeederCount = cfg.Bind(bosses, "Feeding Adds", 3, "Adds a Feeding phase sends to the boss, plus one per extra player within 40 m.");
            FeederPrefab = cfg.Bind(bosses, "Feeding Add Prefab", "Greyling", "The creature a Feeding phase sends when its boss doesn't name one.");
            FeederDistance = cfg.Bind(bosses, "Feeding Distance", 20f, "Metres from the boss the Feeding adds start.");
            FeederHeal = cfg.Bind(bosses, "Feeding Heal", 0.05f, "What each Feeding add heals when it reaches the boss, as a share of the boss's max health. Counter: kill or slow them on the way.");

            const string veteran = "9 - Veteran Abilities";
            VeteranAbilities = cfg.Bind(veteran, "Enabled", true, "Veterans, Elites and Champions get their role's second ability (doc 21): a Caster's Ground Blast, a Brute's Charge, a Skirmisher's Sidestep.");
            GroundBlastCastSeconds = cfg.Bind(veteran, "Ground Blast Cast Seconds", 1.5f, "Wind-up of a Caster's Ground Blast; a red ring marks where its target stood. A stagger or a class stun in this window cancels it. Counter: interrupt, or step out of the ring.");
            GroundBlastCooldown = cfg.Bind(veteran, "Ground Blast Cooldown", 12f, "Seconds between a Caster's Ground Blasts.");
            GroundBlastRadius = cfg.Bind(veteran, "Ground Blast Radius", 3f, "Metres the Ground Blast ring reaches.");
            GroundBlastDamage = cfg.Bind(veteran, "Ground Blast Damage", 0.15f, "Ground Blast damage to each player inside the ring, as a fraction of the caster's max health.");
            ChargeWindupSeconds = cfg.Bind(veteran, "Charge Windup Seconds", 0.8f, "Wind-up of a Brute's Charge (it can't be interrupted); a red ring marks the spot it will slam. Counter: dodge the ring.");
            ChargeCooldown = cfg.Bind(veteran, "Charge Cooldown", 15f, "Seconds between a Brute's Charges.");
            ChargeSpeedBonus = cfg.Bind(veteran, "Charge Speed Bonus", 0.8f, "Extra run speed while a Brute charges (0.8 = +80%).");
            ChargeSlamRadius = cfg.Bind(veteran, "Charge Slam Radius", 3f, "Metres the Charge's slam ring reaches.");
            ChargeSlamDamage = cfg.Bind(veteran, "Charge Slam Damage", 0.08f, "Slam damage to each player inside the ring, as a fraction of the Brute's max health.");
            SidestepCooldown = cfg.Bind(veteran, "Sidestep Cooldown", 6f, "Seconds between a Skirmisher's Sidesteps (it darts aside right after it attacks: hit and run).");
            SidestepDistance = cfg.Bind(veteran, "Sidestep Distance", 4f, "Metres a Skirmisher darts aside.");

            const string packs = "4 - Packs";
            PacksEnabled = cfg.Bind(packs, "Enabled", true, "A natural spawn in a pack biome becomes a pack leader with role-based followers (doc 21). Spawn configs keep working.");
            PacksMeadows = cfg.Bind(packs, "Meadows", true, "Packs in the Meadows: Greylings, Boars and Necks come in groups.");
            PacksBlackForest = cfg.Bind(packs, "Black Forest", true, "Packs in the Black Forest: a Greydwarf leads a Shaman, an Elite and more Greydwarfs.");
            PackFollowersMin = cfg.Bind(packs, "Followers Min", 2, "Fewest followers a pack leader brings.");
            PackFollowersMax = cfg.Bind(packs, "Followers Max", 4, "Most followers a pack leader brings.");
            MaxPacksNearPlayer = cfg.Bind(packs, "Max Packs Near Player", 2, "No new pack forms while this many pack leaders are within 60 m of the nearest player.");

            const string general = "1 - General";
            LockConfiguration = cfg.Bind(general, "Lock Configuration", true, "SERVER. When on, the server's settings and creature rules apply on every player's game. Monsters are run by whichever peer owns them, so a client must use the server's rules, not its own.");
            Enabled = cfg.Bind(general, "Enabled", true, "Turns the whole overhaul on or off.");
            Greydwarfs = cfg.Bind(general, "Greydwarfs", true, "Greydwarfs run on the overhaul brain (the Shaman's heal becomes an interruptible cast).");
            Draugr = cfg.Bind(general, "Draugr", true, "Draugr run on the overhaul brain (the Draugr Elite gets an interruptible war cry).");
            BrainRadius = cfg.Bind(general, "Brain Radius", 40f, "Metres from the nearest player inside which a monster uses the overhaul brain; vanilla AI beyond.");

            const string setup = "8 - Creature Setup";
            CreatureSetupEnabled = cfg.Bind(setup, "Enabled", true, "Applies the rules in BepInEx\\config\\" + Setup.CreatureRules.FileName + " (health, damage and star chances by biome, creature, dungeon, night and bosses defeated) when a monster appears. The shipped file is neutral: nothing changes until a rule line is turned on.");

            const string threat = "2 - Threat";
            ThreatSwitchMelee = cfg.Bind(threat, "Switch Margin Melee", 0.10f, "A monster switches to a new target in melee range only when its threat passes the current target's by this fraction.");
            ThreatSwitchRanged = cfg.Bind(threat, "Switch Margin Ranged", 0.30f, "The same margin for a challenger outside melee range.");
            MeleeRange = cfg.Bind(threat, "Melee Range", 4f, "Metres that count as melee range for the switch margin.");
            ThreatDecaySeconds = cfg.Bind(threat, "Decay Seconds", 10f, "Seconds out of combat after which a monster forgets its threat table.");
            HealThreatFactor = cfg.Bind(threat, "Heal Threat Factor", 0.5f, "Threat per point healed on someone the monster is fighting, relative to damage.");

            const string casts = "3 - Abilities";
            ShamanHealCastSeconds = cfg.Bind(casts, "Shaman Heal Cast Seconds", 2.0f, "Wind-up of the Greydwarf Shaman's heal; staggering it in this window cancels the heal. 2.0 s (Fire 10-07) lets a heavy weapon swung on sight land in time.");
            WarCryCastSeconds = cfg.Bind(casts, "War Cry Cast Seconds", 1.2f, "Wind-up of the Draugr Elite's war cry; staggering it cancels the buff.");
            WarCryCooldown = cfg.Bind(casts, "War Cry Cooldown", 30f, "Seconds between war cries.");
            WarCryRadius = cfg.Bind(casts, "War Cry Radius", 15f, "Metres within which Draugr get the war-cry buff.");
            WarCryDamageBonus = cfg.Bind(casts, "War Cry Damage Bonus", 0.15f, "Extra damage while the war-cry buff lasts (0.15 = +15%).");
            WarCryDuration = cfg.Bind(casts, "War Cry Duration", 10f, "Seconds the war-cry buff lasts.");
        }

        /// <summary>
        /// Every setting syncs from the server under Lock Configuration: monsters run on whichever peer owns them (a
        /// client without FGN Selective), so that peer must roll tiers, affixes and creature setup with the server's values.
        /// </summary>
        public static void BindToSync(ConfigSync sync)
        {
            sync.AddLockingConfigEntry(LockConfiguration);
            var add = typeof(ConfigSync).GetMethod(nameof(ConfigSync.AddConfigEntry));
            foreach (var field in typeof(MonsterConfig).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!(field.GetValue(null) is ConfigEntryBase entry) || entry == LockConfiguration) continue;
                if (!field.FieldType.IsGenericType) continue;
                add.MakeGenericMethod(field.FieldType.GetGenericArguments()[0]).Invoke(sync, new object[] { entry });
            }
        }
    }
}
