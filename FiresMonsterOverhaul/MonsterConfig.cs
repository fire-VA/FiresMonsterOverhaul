using System.Reflection;
using BepInEx.Configuration;
using FiresCore.Sync;

namespace FiresMonsterOverhaul
{
    internal static class MonsterConfig
    {
        public static ConfigEntry<bool> LockConfiguration;
        public static ConfigEntry<bool> CreatureSetupEnabled;
        public static ConfigEntry<float> BossLevelRadius;
        public static ConfigEntry<bool> GearEnabled;
        public static ConfigEntry<string> GearPrefabs, GearPiecesByTier, GearTables, GearFloLuckByTier;
        public static ConfigEntry<float> GearChance, GearDropChance, GearDropChanceChampion, GearDropDurability, GearArmorFactor;
        public static ConfigEntry<bool> SetDropsEnabled;
        public static ConfigEntry<float> SetDropsBadLuckShare, SetDropsLegendaryChance, SetDropsEngageRange;
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
        public static ConfigEntry<string> BonemassFeederPrefab;
        public static ConfigEntry<int> PlagueStacks;
        public static ConfigEntry<float> PlagueSeconds;
        public static ConfigEntry<float> PlagueBurstRadius;
        public static ConfigEntry<float> PlagueBurstDelay;
        public static ConfigEntry<float> PlagueBurstShare;
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
        // 0.0.40 (Fire: boss phases for Moder, Yagluth, The Queen and Fader, on the Eikthyr / Elder / Bonemass system)
        public static ConfigEntry<bool> ModerPhases, YagluthPhases, QueenPhases, FaderPhases;
        public static ConfigEntry<float> ModerFirstPhase, ModerSecondPhase, YagluthFirstPhase, YagluthSecondPhase;
        public static ConfigEntry<float> QueenFirstPhase, QueenSecondPhase, FaderFirstPhase, FaderSecondPhase;
        public static ConfigEntry<int> ModerDrakes, ModerDrakesMax, YagluthFulings, YagluthFulingsMax, QueenSeekers, QueenSeekersMax;
        public static ConfigEntry<int> FaderCharred, FaderCharredMax, FaderHazardRings;
        public static ConfigEntry<string> ModerDrakePrefab, YagluthFulingPrefab, QueenSeekerPrefab, FaderCharredPrefab;
        public static ConfigEntry<float> ModerFrostLaneCooldown, ModerFrostLaneDamage, YagluthFireRingRadius, YagluthFireRingDamage;
        public static ConfigEntry<float> FaderHazardDamage;

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
            const string tiers = "05 - Tiers";
            TiersEnabled = ConfigRanges.Bind(cfg, tiers, "Enabled", true, "Monsters roll a tier when they appear (doc 21): Normal, Veteran, Elite (one affix) or Champion (two affixes and a name). Bosses and tames never do.");
            VeteranChance = ConfigRanges.Bind(cfg, tiers, "Veteran Chance", 0.15f, "Chance a monster is a Veteran (before the distance bonus).");
            EliteChance = ConfigRanges.Bind(cfg, tiers, "Elite Chance", 0.05f, "Chance a monster is an Elite (before the distance bonus).");
            ChampionChance = ConfigRanges.Bind(cfg, tiers, "Champion Chance", 0.01f, "Chance a monster is a Champion (before the distance bonus).");
            TierDistanceBonus = ConfigRanges.Bind(cfg, tiers, "Distance Bonus Per 1000 m", 0.1f, "The tier chances grow by this fraction for every 1000 m from the world centre (0.1 = x1.5 at 5000 m).");
            StarsRaiseTier = ConfigRanges.Bind(cfg, tiers, "Stars Raise Tier", true, "A 1-star monster is at least a Veteran, a 2-star one at least an Elite.");
            AffixesInMeadows = ConfigRanges.Bind(cfg, tiers, "Affixes In Meadows", false, "When off, Meadows monsters stop at Veteran, so the first biome has no affixes.");

            const string affixes = "06 - Affixes";
            MendingAura = ConfigRanges.Bind(cfg, affixes, "Mending Aura", true, "Elites and Champions may get Mending Aura: an interruptible pulse that heals hurt allies nearby. Counter: interrupt it, or pull it away.");
            MendingAuraCastSeconds = ConfigRanges.Bind(cfg, affixes, "Mending Aura Cast Seconds", 1.0f, "Wind-up of each Mending Aura pulse; a stagger or a class stun in this window cancels it.");
            MendingAuraCooldown = ConfigRanges.Bind(cfg, affixes, "Mending Aura Cooldown", 3f, "Seconds between Mending Aura pulses.");
            MendingAuraRadius = ConfigRanges.Bind(cfg, affixes, "Mending Aura Radius", 10f, "Metres within which a Mending Aura pulse heals allies.");
            MendingAuraHeal = ConfigRanges.Bind(cfg, affixes, "Mending Aura Heal", 0.08f, "Health each pulse gives every hurt ally, as a fraction of the ally's max health.");
            Stoneskin = ConfigRanges.Bind(cfg, affixes, "Stoneskin", true, "Elites and Champions may get Stoneskin: much less damage taken until staggered. Counter: stagger it (parries, heavy hits, class stuns).");
            StoneskinDamageReduction = ConfigRanges.Bind(cfg, affixes, "Stoneskin Damage Reduction", 0.8f, "Damage Stoneskin takes off each hit (0.8 = 80 % less). The hit's full stagger still counts.");
            StoneskinCrackedSeconds = ConfigRanges.Bind(cfg, affixes, "Stoneskin Cracked Seconds", 10f, "Seconds a stagger cracks Stoneskin for (full damage), before it hardens again.");
            Enrage = ConfigRanges.Bind(cfg, affixes, "Enrage", true, "Elites and Champions may get Enrage: more damage after a while in combat. Counter: kill it in time.");
            EnrageAfterSeconds = ConfigRanges.Bind(cfg, affixes, "Enrage After Seconds", 45f, "Seconds in combat before an Enrage monster enrages.");
            EnrageDamageBonus = ConfigRanges.Bind(cfg, affixes, "Enrage Damage Bonus", 0.5f, "Extra damage while enraged (0.5 = +50%).");
            EnrageCalmSeconds = ConfigRanges.Bind(cfg, affixes, "Enrage Calm Seconds", 10f, "Seconds out of combat after which an enraged monster calms down and its clock restarts.");
            PackLeader = ConfigRanges.Bind(cfg, affixes, "Pack Leader", true, "Elites and Champions may get Pack Leader: while it fights, allies near it hit harder. Counter: kill it first, or taunt it away from its pack.");
            PackLeaderRadius = ConfigRanges.Bind(cfg, affixes, "Pack Leader Radius", 15f, "Metres within which a Pack Leader inspires its allies.");
            PackLeaderDamageBonus = ConfigRanges.Bind(cfg, affixes, "Pack Leader Damage Bonus", 0.2f, "Extra damage for inspired allies (0.2 = +20%).");
            DeathBurst = ConfigRanges.Bind(cfg, affixes, "Death Burst", true, "Elites and Champions may get Death Burst: on death a red ring marks where it will explode. Counter: step out of the ring.");
            DeathBurstRadius = ConfigRanges.Bind(cfg, affixes, "Death Burst Radius", 4f, "Metres the Death Burst explosion reaches.");
            DeathBurstDelay = ConfigRanges.Bind(cfg, affixes, "Death Burst Delay", 2f, "Seconds between the death and the explosion (the ring shows for this long).");
            DeathBurstDamage = ConfigRanges.Bind(cfg, affixes, "Death Burst Damage", 0.2f, "Explosion damage to each player inside the ring, as a fraction of the monster's max health.");
            Vampiric = ConfigRanges.Bind(cfg, affixes, "Vampiric", true, "Elites and Champions may get Vampiric: they heal from the damage they deal. Counter: anti-heal, or kite it.");
            VampiricHeal = ConfigRanges.Bind(cfg, affixes, "Vampiric Heal", 0.5f, "Health a Vampiric monster gets back, as a fraction of the damage it deals.");
            Shielded = ConfigRanges.Bind(cfg, affixes, "Shielded", true, "Elites and Champions may get Shielded: an absorb shield takes damage before its health does. Counter: burst it down.");
            ShieldHealth = ConfigRanges.Bind(cfg, affixes, "Shield Health", 0.4f, "The shield's strength, as a fraction of the monster's max health.");
            ShieldRechargeSeconds = ConfigRanges.Bind(cfg, affixes, "Shield Recharge Seconds", 15f, "Seconds without taking damage after which a broken or worn shield is full again.");
            Reflective = ConfigRanges.Bind(cfg, affixes, "Reflective", true, "Elites and Champions may get Reflective: now and then it glows for a few seconds and hits taken during the glow are partly reflected back at the attacker. Counter: stop hitting while it glows.");
            ReflectShare = ConfigRanges.Bind(cfg, affixes, "Reflect Share", 0.5f, "Share of each hit taken during the glow that is dealt back to the attacker (it still takes the hit).");
            ReflectSeconds = ConfigRanges.Bind(cfg, affixes, "Reflect Seconds", 3f, "How long each glow lasts.");
            ReflectCooldown = ConfigRanges.Bind(cfg, affixes, "Reflect Cooldown", 12f, "Seconds between glows (counted from the start of one).");
            Frenzied = ConfigRanges.Bind(cfg, affixes, "Frenzied", true, "Elites and Champions may get Frenzied: faster with each ally that dies nearby. Counter: kill it first, or crowd-control it.");
            FrenzySpeedPerAlly = ConfigRanges.Bind(cfg, affixes, "Frenzy Speed Per Ally", 0.15f, "Extra move speed for each ally that died nearby (0.15 = +15%).");
            FrenzyRadius = ConfigRanges.Bind(cfg, affixes, "Frenzy Radius", 15f, "Metres within which an ally's death feeds the frenzy.");
            FrenzyMaxStacks = ConfigRanges.Bind(cfg, affixes, "Frenzy Max Stacks", 5, "Most allies' deaths a frenzy counts.");
            Splitting = ConfigRanges.Bind(cfg, affixes, "Splitting", true, "Elites and Champions may get Splitting: at half health it splits in two (the copy has the same health, no tier, no affixes and drops nothing). Counter: area damage.");
            Blink = ConfigRanges.Bind(cfg, affixes, "Blink", true, "Elites and Champions may get Blink: after a short cast it teleports behind the farthest player it can see who is not its current target (the backline). Counter: interrupt the cast, taunt, crowd control to peel it.");
            BlinkCastSeconds = ConfigRanges.Bind(cfg, affixes, "Blink Cast Seconds", 0.8f, "Wind-up of a Blink; a stagger or a class stun cancels it.");
            BlinkCooldown = ConfigRanges.Bind(cfg, affixes, "Blink Cooldown", 10f, "Seconds between Blinks.");
            BlinkRange = ConfigRanges.Bind(cfg, affixes, "Blink Range", 25f, "Metres within which a Blink picks its player.");
            Warded = ConfigRanges.Bind(cfg, affixes, "Warded", true, "Elites and Champions may get Warded: when it first fights it raises a ward totem nearby and takes no damage while the totem stands within range. Counter: break the totem (switch targets, area damage).");
            WardRange = ConfigRanges.Bind(cfg, affixes, "Ward Range", 20f, "Metres from its totem within which a Warded monster is immune.");
            WardHealth = ConfigRanges.Bind(cfg, affixes, "Ward Health", 0.25f, "The ward totem's health, as a fraction of the monster's max health (at least 50).");
            WardTotemScale = ConfigRanges.Bind(cfg, affixes, "Ward Totem Scale", 3f, "Size of the ward totem (the vanilla Fuling totem's model, scaled up).");
            Summoner = ConfigRanges.Bind(cfg, affixes, "Summoner", true, "Elites and Champions may get Summoner: an interruptible call that brings weaker copies of itself (half health, no tier, no drops). Counter: interrupt the call, area damage on the adds.");
            SummonCastSeconds = ConfigRanges.Bind(cfg, affixes, "Summon Cast Seconds", 2f, "Wind-up of a Summoner's call; a stagger or a class stun cancels it.");
            SummonCooldown = ConfigRanges.Bind(cfg, affixes, "Summon Cooldown", 20f, "Seconds between a Summoner's calls.");
            SummonCount = ConfigRanges.Bind(cfg, affixes, "Summon Count", 2, "Adds each call brings.");
            SummonMaxAlive = ConfigRanges.Bind(cfg, affixes, "Summon Max Alive", 4, "Most of a Summoner's adds alive at once (no call while it has this many).");
            Hexer = ConfigRanges.Bind(cfg, affixes, "Hexer", true, "Elites and Champions may get Hexer: an interruptible cast that curses its target (less damage dealt, slower). Counter: interrupt it, or cleanse the curse.");
            HexCastSeconds = ConfigRanges.Bind(cfg, affixes, "Hex Cast Seconds", 1.2f, "Wind-up of a Hexer's curse; a stagger or a class stun cancels it.");
            HexCooldown = ConfigRanges.Bind(cfg, affixes, "Hex Cooldown", 15f, "Seconds between a Hexer's curses.");
            HexSeconds = ConfigRanges.Bind(cfg, affixes, "Hex Seconds", 8f, "How long the curse lasts.");
            HexDamagePenalty = ConfigRanges.Bind(cfg, affixes, "Hex Damage Penalty", 0.3f, "Less damage dealt while cursed (0.3 = -30%).");
            HexSlow = ConfigRanges.Bind(cfg, affixes, "Hex Slow", 0.2f, "Slower movement while cursed (0.2 = -20%).");
            Elemental = ConfigRanges.Bind(cfg, affixes, "Elemental", true, "Elites and Champions may get Molten / Frostbound / Plagued (the element follows its biome): while it fights it leaves rings of burning, freezing or poisoned ground. Counter: move out; cleanse the effect.");
            HazardInterval = ConfigRanges.Bind(cfg, affixes, "Hazard Interval", 3f, "Seconds between the hazard rings an Elemental monster leaves.");
            HazardSeconds = ConfigRanges.Bind(cfg, affixes, "Hazard Seconds", 8f, "How long each hazard ring lasts.");
            HazardRadius = ConfigRanges.Bind(cfg, affixes, "Hazard Radius", 2.5f, "Metres each hazard ring reaches.");
            HazardDamage = ConfigRanges.Bind(cfg, affixes, "Hazard Damage", 0.02f, "Elemental damage per second to a player standing in a ring, as a fraction of the monster's max health (fire burns, frost chills, poison poisons: vanilla's own effects).");
            SpiritLinked = ConfigRanges.Bind(cfg, affixes, "Spirit Linked", true, "Elites and Champions may get Spirit-linked: it comes with a twin; the two share damage, and the survivor brings the other back unless both die within a few seconds of each other. Counter: burst them down together.");
            SpiritLinkShare = ConfigRanges.Bind(cfg, affixes, "Spirit Link Share", 0.5f, "Share of each hit that goes to the twin instead.");
            SpiritLinkReviveSeconds = ConfigRanges.Bind(cfg, affixes, "Spirit Link Revive Seconds", 10f, "Seconds after one twin dies before the survivor brings it back (both must die within this window).");

            const string champions = "07 - Champions";
            ChampionPhase = ConfigRanges.Bind(cfg, champions, "Half Health Phase", true, "At half health a Champion stops for an uninterruptible rally (a burst window) and gains one more affix (doc 21).");
            ChampionPhaseCastSeconds = ConfigRanges.Bind(cfg, champions, "Rally Cast Seconds", 1.5f, "Length of the Champion's half-health rally; it stands still and can't be interrupted.");

            const string bosses = "10 - Bosses";
            Bosses = ConfigRanges.Bind(cfg, bosses, "Enabled", true, "Vanilla bosses get phases, new attacks, healing phases and a soft enrage (doc 22). Eikthyr: Thunderhead, a charge lane, spirit stags. The Elder: Entangle, Tethered Heartwood. Moder, Yagluth, The Queen and Fader: two phases each (their own settings below).");
            BossEnrageSeconds = ConfigRanges.Bind(cfg, bosses, "Enrage Seconds", 480f, "Seconds in combat after which a boss enrages (soft enrage).");
            BossEnrageDamageBonus = ConfigRanges.Bind(cfg, bosses, "Enrage Damage Bonus", 0.5f, "Extra damage once a boss has enraged (0.5 = +50%).");
            BossInterruptShare = ConfigRanges.Bind(cfg, bosses, "Interrupt Share", 0.06f, "Bosses can't be staggered, so their interruptible casts (Thunderhead, channelled heals) break on heavy hits instead: the hits' stagger damage during one cast, as a share of the boss's max health. Blunt and heavy (secondary) attacks count most.");
            ThunderheadCooldown = ConfigRanges.Bind(cfg, bosses, "Eikthyr Thunderhead Cooldown", 25f, "Seconds between Eikthyr's Thunderheads.");
            ThunderheadCastSeconds = ConfigRanges.Bind(cfg, bosses, "Eikthyr Thunderhead Cast Seconds", 3f, "Wind-up of Thunderhead; a stagger (a parry, a heavy hit, Shield Bash) or a class stun in this window cancels it. The teaching fight's lesson.");
            ThunderheadRadius = ConfigRanges.Bind(cfg, bosses, "Eikthyr Thunderhead Radius", 6f, "Metres the Thunderhead ring reaches around Eikthyr.");
            ThunderheadDamage = ConfigRanges.Bind(cfg, bosses, "Eikthyr Thunderhead Damage", 0.25f, "Thunderhead's damage to each player in the ring, as a share of that player's max health.");
            ChargeLaneCooldown = ConfigRanges.Bind(cfg, bosses, "Eikthyr Charge Lane Cooldown", 18f, "Seconds between Eikthyr's charge lanes.");
            ChargeLaneWindupSeconds = ConfigRanges.Bind(cfg, bosses, "Eikthyr Charge Lane Windup Seconds", 1.2f, "Seconds the lane shows before Eikthyr charges down it.");
            ChargeLaneWidth = ConfigRanges.Bind(cfg, bosses, "Eikthyr Charge Lane Width", 2f, "Width of the charge lane in metres.");
            ChargeLaneDamage = ConfigRanges.Bind(cfg, bosses, "Eikthyr Charge Lane Damage", 0.2f, "Damage to each player still in the lane when the charge ends, as a share of that player's max health.");
            EikthyrSpiritStags = ConfigRanges.Bind(cfg, bosses, "Eikthyr Spirit Stags", 2, "Spirit stags Eikthyr calls at half health (small crystal copies of him), plus one per extra player within 40 m.");
            EikthyrSpiritStagsMax = ConfigRanges.Bind(cfg, bosses, "Eikthyr Spirit Stags Max", 4, "Most spirit stags Eikthyr calls, however many players.");
            SpiritHealth = ConfigRanges.Bind(cfg, bosses, "Spirit Health", 0.1f, "A boss spirit's health, as a share of its boss's.");
            SpiritScale = ConfigRanges.Bind(cfg, bosses, "Spirit Scale", 0.4f, "A boss spirit's size relative to its boss.");
            EntangleCooldown = ConfigRanges.Bind(cfg, bosses, "Elder Entangle Cooldown", 20f, "Seconds between the Elder's Entangles.");
            EntangleWindupSeconds = ConfigRanges.Bind(cfg, bosses, "Elder Entangle Windup Seconds", 1.2f, "Seconds the ring shows at a player's feet before the roots come (it can't be interrupted). Counter: step out of the ring.");
            EntangleRadius = ConfigRanges.Bind(cfg, bosses, "Elder Entangle Radius", 2.5f, "Metres the Entangle ring reaches.");
            EntangleSeconds = ConfigRanges.Bind(cfg, bosses, "Elder Entangle Seconds", 12f, "Longest a player stays rooted if nobody breaks the roots. Purify cleanses it.");
            EntangleRootsHealth = ConfigRanges.Bind(cfg, bosses, "Elder Entangle Roots Health", 60f, "Health of the roots that hold a player (anyone can break them, the rooted player too).");
            BonemassFeederPrefab = ConfigRanges.Bind(cfg, bosses, "Bonemass Feeding Add Prefab", "Blob", "What crawls to Bonemass at two thirds and one third health and heals him on arrival. Counter: kill or slow them on the way.");
            PlagueStacks = ConfigRanges.Bind(cfg, bosses, "Bonemass Plague Stacks", 5, "Each hit Bonemass lands adds a Plague stack; at this many it bursts on everyone near the player. Counter: cleanse it (Purify) before then.");
            PlagueSeconds = ConfigRanges.Bind(cfg, bosses, "Bonemass Plague Seconds", 30f, "Seconds the Plague lasts after his last hit (then the stacks are gone).");
            PlagueBurstRadius = ConfigRanges.Bind(cfg, bosses, "Bonemass Plague Burst Radius", 5f, "Metres the Plague burst reaches. Counter: spread out.");
            PlagueBurstDelay = ConfigRanges.Bind(cfg, bosses, "Bonemass Plague Burst Delay", 2.5f, "Seconds the burst ring shows before it goes off.");
            PlagueBurstShare = ConfigRanges.Bind(cfg, bosses, "Bonemass Plague Burst Share", 0.15f, "Damage of the burst, as a share of each hit player's max health.");
            TetherCount = ConfigRanges.Bind(cfg, bosses, "Tethered Sources", 3, "Heartwood saplings (or a later boss's sources) raised by a Tethered phase. The Elder's comes at half health.");
            TetherRadius = ConfigRanges.Bind(cfg, bosses, "Tethered Radius", 12f, "Metres from the boss the Tethered sources stand.");
            TetherHealth = ConfigRanges.Bind(cfg, bosses, "Tethered Source Health", 0.04f, "Each Tethered source's health, as a share of the boss's max health (at least 50).");
            TetherHealPerSecond = ConfigRanges.Bind(cfg, bosses, "Tethered Heal Per Second", 0.01f, "The boss's heal per second while any Tethered source stands, as a share of its max health. It's immune meanwhile.");
            HeartwoodScale = ConfigRanges.Bind(cfg, bosses, "Heartwood Scale", 4f, "Size of the Heartwood sapling (the vanilla beech sapling's model, scaled up).");
            ChannelPhaseSeconds = ConfigRanges.Bind(cfg, bosses, "Channelled Phase Seconds", 40f, "How long a Channelled phase lasts.");
            ChannelEvery = ConfigRanges.Bind(cfg, bosses, "Channelled Every", 12f, "Seconds between a Channelled phase's heal casts.");
            ChannelCastSeconds = ConfigRanges.Bind(cfg, bosses, "Channelled Cast Seconds", 5f, "Wind-up of a channelled heal; a stagger or a class stun cancels it. Counter: interrupt every cast.");
            ChannelHeal = ConfigRanges.Bind(cfg, bosses, "Channelled Heal", 0.1f, "What a finished channelled heal restores, as a share of the boss's max health.");
            ShellShare = ConfigRanges.Bind(cfg, bosses, "Shelled Shield", 0.15f, "A Shelled phase's absorb shield, as a share of the boss's max health.");
            ShellSeconds = ConfigRanges.Bind(cfg, bosses, "Shelled Seconds", 15f, "Seconds to break the shell before the boss heals.");
            ShellHeal = ConfigRanges.Bind(cfg, bosses, "Shelled Heal", 0.15f, "What the boss heals if its shell holds, as a share of its max health.");
            FeederCount = ConfigRanges.Bind(cfg, bosses, "Feeding Adds", 3, "Adds a Feeding phase sends to the boss, plus one per extra player within 40 m.");
            FeederPrefab = ConfigRanges.Bind(cfg, bosses, "Feeding Add Prefab", "Greyling", "The creature a Feeding phase sends when its boss doesn't name one.");
            FeederDistance = ConfigRanges.Bind(cfg, bosses, "Feeding Distance", 20f, "Metres from the boss the Feeding adds start.");
            FeederHeal = ConfigRanges.Bind(cfg, bosses, "Feeding Heal", 0.05f, "What each Feeding add heals when it reaches the boss, as a share of the boss's max health. Counter: kill or slow them on the way.");
            // 0.0.40 (Fire: phases for Moder, Yagluth, The Queen and Fader; default ON like the first three bosses' phases)
            var phaseRange = new AcceptableValueRange<float>(0.01f, 0.99f);
            ModerPhases = ConfigRanges.Bind(cfg, bosses, "Moder Phases", true, "Moder's phases: at the first threshold drakes come to her aid, from the second she breathes frost lanes (a lane shows, then freezing ground along it). Counter: kill the drakes, step out of the lane.");
            ModerFirstPhase = ConfigRanges.Bind(cfg, bosses, "Moder First Phase Health", 0.66f, new ConfigDescription("Health share at which Moder calls her drakes.", phaseRange));
            ModerSecondPhase = ConfigRanges.Bind(cfg, bosses, "Moder Second Phase Health", 0.33f, new ConfigDescription("Health share at which Moder starts her frost lanes.", phaseRange));
            ModerDrakes = ConfigRanges.Bind(cfg, bosses, "Moder Drakes", 2, "Drakes Moder calls at her first phase, plus one per extra player within 40 m.");
            ModerDrakesMax = ConfigRanges.Bind(cfg, bosses, "Moder Drakes Max", 5, "Most drakes Moder calls, however many players.");
            ModerDrakePrefab = ConfigRanges.Bind(cfg, bosses, "Moder Drake Prefab", "Hatchling", "The creature Moder calls (the vanilla drake is Hatchling).");
            ModerFrostLaneCooldown = ConfigRanges.Bind(cfg, bosses, "Moder Frost Lane Cooldown", 20f, "Seconds between Moder's frost lanes from her second phase on (the first comes as the phase starts).");
            ModerFrostLaneDamage = ConfigRanges.Bind(cfg, bosses, "Moder Frost Lane Damage Per Second", 12f, "Frost damage per second to a player on the lane's freezing ground (vanilla frost, so Frost Resistance helps).");
            YagluthPhases = ConfigRanges.Bind(cfg, bosses, "Yagluth Phases", true, "Yagluth's phases: at the first threshold a fire ring bursts round him, at the second Fulings come to his aid. Counter: step out of the ring, kill the Fulings.");
            YagluthFirstPhase = ConfigRanges.Bind(cfg, bosses, "Yagluth First Phase Health", 0.66f, new ConfigDescription("Health share at which Yagluth's fire ring bursts.", phaseRange));
            YagluthSecondPhase = ConfigRanges.Bind(cfg, bosses, "Yagluth Second Phase Health", 0.33f, new ConfigDescription("Health share at which Yagluth calls his Fulings.", phaseRange));
            YagluthFireRingRadius = ConfigRanges.Bind(cfg, bosses, "Yagluth Fire Ring Radius", 10f, "Metres the fire ring reaches round Yagluth (it shows for 2 s first).");
            YagluthFireRingDamage = ConfigRanges.Bind(cfg, bosses, "Yagluth Fire Ring Damage Share", 0.3f, "The fire ring's damage, as a share of each hit player's max health.");
            YagluthFulings = ConfigRanges.Bind(cfg, bosses, "Yagluth Fulings", 3, "Fulings Yagluth calls at his second phase, plus one per extra player within 40 m.");
            YagluthFulingsMax = ConfigRanges.Bind(cfg, bosses, "Yagluth Fulings Max", 6, "Most Fulings Yagluth calls, however many players.");
            YagluthFulingPrefab = ConfigRanges.Bind(cfg, bosses, "Yagluth Fuling Prefab", "Goblin", "The creature Yagluth calls (the vanilla Fuling is Goblin).");
            QueenPhases = ConfigRanges.Bind(cfg, bosses, "Queen Phases", true, "The Queen's phases: at the first threshold Seekers come to her aid, from the second she Entangles players (the Elder's Entangle settings). Counter: kill the Seekers, step out of the ring, break the roots.");
            QueenFirstPhase = ConfigRanges.Bind(cfg, bosses, "Queen First Phase Health", 0.66f, new ConfigDescription("Health share at which the Queen calls her Seekers.", phaseRange));
            QueenSecondPhase = ConfigRanges.Bind(cfg, bosses, "Queen Second Phase Health", 0.33f, new ConfigDescription("Health share at which the Queen starts Entangling.", phaseRange));
            QueenSeekers = ConfigRanges.Bind(cfg, bosses, "Queen Seekers", 3, "Seekers the Queen calls at her first phase, plus one per extra player within 40 m.");
            QueenSeekersMax = ConfigRanges.Bind(cfg, bosses, "Queen Seekers Max", 6, "Most Seekers the Queen calls, however many players.");
            QueenSeekerPrefab = ConfigRanges.Bind(cfg, bosses, "Queen Seeker Prefab", "Seeker", "The creature the Queen calls (vanilla: Seeker).");
            FaderPhases = ConfigRanges.Bind(cfg, bosses, "Fader Phases", true, "Fader's phases: at the first threshold a field of burning rings spreads round him, at the second Charred come to his aid. Counter: keep off the burning rings, kill the Charred.");
            FaderFirstPhase = ConfigRanges.Bind(cfg, bosses, "Fader First Phase Health", 0.66f, new ConfigDescription("Health share at which Fader's burning field spreads.", phaseRange));
            FaderSecondPhase = ConfigRanges.Bind(cfg, bosses, "Fader Second Phase Health", 0.33f, new ConfigDescription("Health share at which Fader calls his Charred.", phaseRange));
            FaderHazardRings = ConfigRanges.Bind(cfg, bosses, "Fader Hazard Rings", 6, "Burning rings in Fader's field (each the Elemental affix's ring size and length).");
            FaderHazardDamage = ConfigRanges.Bind(cfg, bosses, "Fader Hazard Damage Per Second", 20f, "Fire damage per second to a player standing in one of Fader's burning rings.");
            FaderCharred = ConfigRanges.Bind(cfg, bosses, "Fader Charred", 3, "Charred Fader calls at his second phase, plus one per extra player within 40 m.");
            FaderCharredMax = ConfigRanges.Bind(cfg, bosses, "Fader Charred Max", 6, "Most Charred Fader calls, however many players.");
            FaderCharredPrefab = ConfigRanges.Bind(cfg, bosses, "Fader Charred Prefab", "Charred_Melee", "The creature Fader calls (vanilla: Charred_Melee).");

            const string veteran = "09 - Veteran Abilities";
            VeteranAbilities = ConfigRanges.Bind(cfg, veteran, "Enabled", true, "Veterans, Elites and Champions get their role's second ability (doc 21): a Caster's Ground Blast, a Brute's Charge, a Skirmisher's Sidestep.");
            GroundBlastCastSeconds = ConfigRanges.Bind(cfg, veteran, "Ground Blast Cast Seconds", 1.5f, "Wind-up of a Caster's Ground Blast; a red ring marks where its target stood. A stagger or a class stun in this window cancels it. Counter: interrupt, or step out of the ring.");
            GroundBlastCooldown = ConfigRanges.Bind(cfg, veteran, "Ground Blast Cooldown", 12f, "Seconds between a Caster's Ground Blasts.");
            GroundBlastRadius = ConfigRanges.Bind(cfg, veteran, "Ground Blast Radius", 3f, "Metres the Ground Blast ring reaches.");
            GroundBlastDamage = ConfigRanges.Bind(cfg, veteran, "Ground Blast Damage", 0.15f, "Ground Blast damage to each player inside the ring, as a fraction of the caster's max health.");
            ChargeWindupSeconds = ConfigRanges.Bind(cfg, veteran, "Charge Windup Seconds", 0.8f, "Wind-up of a Brute's Charge (it can't be interrupted); a red ring marks the spot it will slam. Counter: dodge the ring.");
            ChargeCooldown = ConfigRanges.Bind(cfg, veteran, "Charge Cooldown", 15f, "Seconds between a Brute's Charges.");
            ChargeSpeedBonus = ConfigRanges.Bind(cfg, veteran, "Charge Speed Bonus", 0.8f, "Extra run speed while a Brute charges (0.8 = +80%).");
            ChargeSlamRadius = ConfigRanges.Bind(cfg, veteran, "Charge Slam Radius", 3f, "Metres the Charge's slam ring reaches.");
            ChargeSlamDamage = ConfigRanges.Bind(cfg, veteran, "Charge Slam Damage", 0.08f, "Slam damage to each player inside the ring, as a fraction of the Brute's max health.");
            SidestepCooldown = ConfigRanges.Bind(cfg, veteran, "Sidestep Cooldown", 6f, "Seconds between a Skirmisher's Sidesteps (it darts aside right after it attacks: hit and run).");
            SidestepDistance = ConfigRanges.Bind(cfg, veteran, "Sidestep Distance", 4f, "Metres a Skirmisher darts aside.");

            const string packs = "04 - Packs";
            PacksEnabled = ConfigRanges.Bind(cfg, packs, "Enabled", true, "A natural spawn in a pack biome becomes a pack leader with role-based followers (doc 21). Spawn configs keep working.");
            PacksMeadows = ConfigRanges.Bind(cfg, packs, "Meadows", true, "Packs in the Meadows: Greylings, Boars and Necks come in groups.");
            PacksBlackForest = ConfigRanges.Bind(cfg, packs, "Black Forest", true, "Packs in the Black Forest: a Greydwarf leads a Shaman, an Elite and more Greydwarfs.");
            PackFollowersMin = ConfigRanges.Bind(cfg, packs, "Followers Min", 2, "Fewest followers a pack leader brings.");
            PackFollowersMax = ConfigRanges.Bind(cfg, packs, "Followers Max", 4, "Most followers a pack leader brings.");
            MaxPacksNearPlayer = ConfigRanges.Bind(cfg, packs, "Max Packs Near Player", 2, "No new pack forms while this many pack leaders are within 60 m of the nearest player.");

            const string general = "01 - General";
            LockConfiguration = ConfigRanges.Bind(cfg, general, "Lock Configuration", true, "SERVER. When on, the server's settings and creature rules apply on every player's game. Monsters are run by whichever peer owns them, so a client must use the server's rules, not its own.");
            Enabled = ConfigRanges.Bind(cfg, general, "Enabled", true, "Turns the whole overhaul on or off.");
            Greydwarfs = ConfigRanges.Bind(cfg, general, "Greydwarfs", true, "Greydwarfs run on the overhaul brain (the Shaman's heal becomes an interruptible cast).");
            Draugr = ConfigRanges.Bind(cfg, general, "Draugr", true, "Draugr run on the overhaul brain (the Draugr Elite gets an interruptible war cry).");
            BrainRadius = ConfigRanges.Bind(cfg, general, "Brain Radius", 40f, "Metres from the nearest player inside which a monster uses the overhaul brain; vanilla AI beyond.");

            const string setup = "08 - Creature Setup";
            CreatureSetupEnabled = ConfigRanges.Bind(cfg, setup, "Enabled", true, "Applies the rules in BepInEx\\config\\" + Setup.CreatureRules.FileName + " (health, damage and star chances by biome, creature, dungeon, night and bosses defeated) when a monster appears. The shipped file is neutral: nothing changes until a rule line is turned on.");
            BossLevelRadius = ConfigRanges.Bind(cfg, setup, "Boss Level Player Radius", 100f, new ConfigDescription("A new monster's boss level is the highest boss tier (1 Eikthyr .. 7 Fader) any player within this many metres has defeated, from RPGmaker's private keys (works when the server owns the monster). Without RPGmaker or private keys: the world's global boss keys. ADMIN, server-authoritative.", new AcceptableValueRange<float>(10f, 500f)));
            const string gear = "12 - Monster Gear";
            GearEnabled = ConfigRanges.Bind(cfg, gear, "Enabled", false, "OFF until Fire's go (after the helmet bone-rotation fix; fmo_gear works while off). Draugr and Skeletons can wear real player armour (Tools\\FMO_MONSTER_GEAR_DESIGN.md): rolled once per monster, it reduces their damage taken and can drop on death.");
            GearPrefabs = ConfigRanges.Bind(cfg, gear, "Prefabs", "Draugr,Draugr_Elite,Draugr_Ranged,Skeleton,Skeleton_Poison,Skeleton_NoArcher", "Which monsters may wear gear (comma list of prefab names). Phase 1 bodies: Draugr*, Skeleton*.");
            GearChance = ConfigRanges.Bind(cfg, gear, "Chance", 0.35f, "Chance that an eligible monster rolls gear at all.");
            GearPiecesByTier = ConfigRanges.Bind(cfg, gear, "Pieces By Tier", "0-1,1-2,2-3,3-3", "How many armour pieces, min-max, for Normal, Veteran, Elite, Champion (only the slots the body can show are ever filled).");
            GearTables = ConfigRanges.Bind(cfg, gear, "Gear Tables", "Meadows=HelmetLeather,ArmorLeatherChest,ArmorLeatherLegs;BlackForest=HelmetBronze,ArmorBronzeChest,ArmorBronzeLegs;Swamp=HelmetIron,ArmorIronChest,ArmorIronLegs;Mountain=HelmetDrake,ArmorWolfChest,ArmorWolfLegs;Plains=HelmetPadded,ArmorPaddedCuirass,ArmorPaddedGreaves;Mistlands=HelmetCarapace,ArmorCarapaceChest,ArmorCarapaceLegs;AshLands=HelmetFlametal,ArmorFlametalChest,ArmorFlametalLegs;DeepNorth=HelmetFlametal,ArmorFlametalChest,ArmorFlametalLegs", "Biome=item,item,...; the armour set a monster draws from in each biome (vanilla item prefab names).");
            GearFloLuckByTier = ConfigRanges.Bind(cfg, gear, "Loot Luck By Tier", "1,1,1.25,1.5", "Fires Loot Overhaul roll luck for Normal, Veteran, Elite, Champion. Normal monsters never roll rarity (plain gear).");
            GearDropChance = ConfigRanges.Bind(cfg, gear, "Drop Chance", 0.05f, "Chance that each worn piece drops when the monster dies.");
            GearDropChanceChampion = ConfigRanges.Bind(cfg, gear, "Champion Drop Chance", 0.15f, "Drop Chance for Champions.");
            GearDropDurability = ConfigRanges.Bind(cfg, gear, "Drop Durability", 0.5f, "A dropped piece keeps this share of its maximum durability.");
            GearArmorFactor = ConfigRanges.Bind(cfg, gear, "Armour Factor", 1f, "Multiplier on the worn armour's damage reduction (used when Fires Loot Overhaul isn't installed; FLO applies it otherwise).");

            const string sets = "13 - Set Drops";
            SetDropsEnabled = ConfigRanges.Bind(cfg, sets, "Enabled", true, "ON by default (does nothing without Fires Loot Overhaul). Fires Loot Overhaul set pieces drop from the sources in FiresMonsterOverhaul.set_drops.yaml (minibosses, dungeon bosses, area mobs, Champions). Personal loot: every engaged player rolls their own chance. Needs FLO 0.1.2+ with [Sets] Enabled.");
            // 0.0.41 (Fire 10-20 via [fgn]: "set pieces must actually drop"): default ON; a cfg still on the old default OFF is turned ON
            // once (record: <BepInEx>\cache\FiresMonsterOverhaul\migrations.txt), so a later OFF chosen on purpose is kept.
            try
            {
                string mig = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "cache", "FiresMonsterOverhaul", "migrations.txt");
                string done = System.IO.File.Exists(mig) ? System.IO.File.ReadAllText(mig).Trim() : "";
                const string Id = "setdrops-default-on";
                if (!("," + done + ",").Contains("," + Id + ","))
                {
                    if (!SetDropsEnabled.Value) { SetDropsEnabled.Value = true; MonsterLog.Info("[13 - Set Drops] Enabled was still the old default OFF; now ON (the new default)."); }
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(mig));
                    System.IO.File.WriteAllText(mig, string.IsNullOrEmpty(done) ? Id : done + "," + Id);
                }
            }
            catch (System.Exception e) { MonsterLog.Debug("set drops default migration: " + e.Message); }
            SetDropsBadLuckShare = ConfigRanges.Bind(cfg, sets, "Bad Luck Share", 0.25f, "Each kill of a set's source without a drop adds the set's chance x this to that player's next chance for that set (0.25: a 35% boss +8.75% per miss, a 0.5% area set +0.125%); a drop resets it. A set's bad_luck_step in the YAML overrides it.");
            SetDropsLegendaryChance = ConfigRanges.Bind(cfg, sets, "Legendary Chance", 0.15f, "Chance a set piece is Legendary instead of Epic.");
            SetDropsEngageRange = ConfigRanges.Bind(cfg, sets, "Engage Range", 40f, "Metres: players this close to the kill who are on its threat table (or within a third of this) count as engaged.");

            const string threat = "02 - Threat";
            ThreatSwitchMelee = ConfigRanges.Bind(cfg, threat, "Switch Margin Melee", 0.10f, "A monster switches to a new target in melee range only when its threat passes the current target's by this fraction.");
            ThreatSwitchRanged = ConfigRanges.Bind(cfg, threat, "Switch Margin Ranged", 0.30f, "The same margin for a challenger outside melee range.");
            MeleeRange = ConfigRanges.Bind(cfg, threat, "Melee Range", 4f, "Metres that count as melee range for the switch margin.");
            ThreatDecaySeconds = ConfigRanges.Bind(cfg, threat, "Decay Seconds", 10f, "Seconds out of combat after which a monster forgets its threat table.");
            HealThreatFactor = ConfigRanges.Bind(cfg, threat, "Heal Threat Factor", 0.5f, "Threat per point healed on someone the monster is fighting, relative to damage.");

            const string casts = "03 - Abilities";
            ShamanHealCastSeconds = ConfigRanges.Bind(cfg, casts, "Shaman Heal Cast Seconds", 2.0f, "Wind-up of the Greydwarf Shaman's heal; staggering it in this window cancels the heal. 2.0 s (Fire 10-07) lets a heavy weapon swung on sight land in time.");
            WarCryCastSeconds = ConfigRanges.Bind(cfg, casts, "War Cry Cast Seconds", 1.2f, "Wind-up of the Draugr Elite's war cry; staggering it cancels the buff.");
            WarCryCooldown = ConfigRanges.Bind(cfg, casts, "War Cry Cooldown", 30f, "Seconds between war cries.");
            WarCryRadius = ConfigRanges.Bind(cfg, casts, "War Cry Radius", 15f, "Metres within which Draugr get the war-cry buff.");
            WarCryDamageBonus = ConfigRanges.Bind(cfg, casts, "War Cry Damage Bonus", 0.15f, "Extra damage while the war-cry buff lasts (0.15 = +15%).");
            WarCryDuration = ConfigRanges.Bind(cfg, casts, "War Cry Duration", 10f, "Seconds the war-cry buff lasts.");
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
