using FiresMonsterOverhaul.Scaling;

namespace FiresMonsterOverhaul.Bosses
{
    /// <summary>The boss scripts' view of group scaling (Scaling\GroupScaler.cs): hooks for per-group-size mechanics and difficulty levels.</summary>
    public sealed partial class BossDirector
    {
        /// <summary>Engaged players (companions count half), as last written by the boss's owner. For per-group-size mechanics.</summary>
        public float GroupSize => GroupScaler.GroupSizeOf(Nview);

        /// <summary>The difficulty level (0 = Normal). A hook for later boss difficulties; nothing sets it yet.</summary>
        public int Difficulty => GroupScaler.DifficultyOf(Nview);
    }
}
