namespace FiresMonsterOverhaul.Roles
{
    /// <summary>Doc 21's eight roles. None = no role (a plain creature, or a boss: bosses get phases instead).</summary>
    public enum MonsterRole
    {
        None = 0,
        Brute = 1,       // big telegraphed slams, heavy stagger resistance        -> dodge the ring; taunt it off casters
        Skirmisher = 2,  // fast, flanks, hits and runs                            -> slows, roots, AoE
        Shaman = 3,      // heals and wards its pack (interruptible)                -> interrupt, anti-heal, kill first
        Caster = 4,      // ranged area attacks with rings                          -> interrupt, close the gap
        Summoner = 5,    // calls adds (interruptible)                              -> interrupt, AoE the adds
        Warlord = 6,     // war cries that buff the pack                            -> kill first, or taunt it clear
        Ambusher = 7,    // stealthed; opens with a burst                           -> detection, heals, defensives
        Bombardier = 8,  // lobbed hazards; bursts on death                         -> spread out, move
    }
}
