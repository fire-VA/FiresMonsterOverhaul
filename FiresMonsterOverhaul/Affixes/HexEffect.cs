namespace FiresMonsterOverhaul.Affixes
{
    /// <summary>
    /// The Hexer's curse: less damage dealt and slower, for Hex Seconds. Registered in ObjectDB on every peer (the
    /// ObjectDB.Awake patch), so SEMan.AddStatusEffect by name hash works on whichever peer owns the cursed player; it
    /// reads its numbers from the (server-synced) config when it lands.
    /// Cleanse: Core's Purify removes debuffs by name; "FMO_Hex" has to be on its list ([generator], asked 10-08).
    /// </summary>
    public sealed class HexEffect : SE_Stats
    {
        public const string EffectName = "FMO_Hex";
        public static readonly int Hash = EffectName.GetStableHashCode();

        public override void Setup(Character character)
        {
            m_ttl = MonsterConfig.HexSeconds.Value;
            m_speedModifier = -MonsterConfig.HexSlow.Value;
            m_modifyAttackSkill = Skills.SkillType.All;
            m_damageModifier = 1f - MonsterConfig.HexDamagePenalty.Value;
            base.Setup(character);
        }

        public static void Register(ObjectDB db)
        {
            if (db == null || db.m_StatusEffects == null) return;
            if (db.m_StatusEffects.Exists(s => s != null && s.name == EffectName)) return;
            var se = CreateInstance<HexEffect>();
            se.name = EffectName;
            se.m_name = "Hexed";
            se.m_tooltip = "Cursed by a Hexer: you deal less damage and move slower. Cleanse it.";
            var poison = db.GetStatusEffect("Poison".GetStableHashCode());
            if (poison != null) se.m_icon = poison.m_icon;
            db.m_StatusEffects.Add(se);
            PurifyHook.Register(EffectName);
        }
    }
}
