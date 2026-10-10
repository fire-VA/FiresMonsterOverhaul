using System.Collections.Generic;
using System.Text;
using FiresCore.Npc.Archetypes.Effects;
using FiresMonsterOverhaul.Affixes;
using FiresMonsterOverhaul.Agent;
using FiresMonsterOverhaul.Casts;
using FiresMonsterOverhaul.Tiers;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace FiresMonsterOverhaul.Ui
{
    /// <summary>
    /// Every viewer, on every overhaul monster's nameplate, all read from the synced ZDO:
    /// a cast bar under the health bar while it casts (gold = interruptible, grey = not); once per new cast, the
    /// ground-ring telegraph (monster rings use caster id 0, so every viewer that may see them draws them); and, for a
    /// Veteran, Elite or Champion (or an ally a Pack Leader inspires), a tier line above the name with its affixes and
    /// their state; for a boss in a healing pattern, the pattern and its state.
    /// </summary>
    [HarmonyPatch(typeof(EnemyHud), nameof(EnemyHud.UpdateHuds))]
    internal static class CastBarHud
    {
        private static readonly Color Gold = new Color(1f, 0.78f, 0.2f, 1f);
        private static readonly Color Grey = new Color(0.6f, 0.6f, 0.6f, 1f);
        private const float TierFontScale = 0.75f;
        private const float TierLineGap = 2f;

        private sealed class BarState
        {
            public GuiBar Bar;
            public int Seq = -1;
            public float LocalStart;
            public TextMeshProUGUI TierLine;
            public string TierText;
        }

        private static readonly Dictionary<Character, BarState> Bars = new Dictionary<Character, BarState>();
        private static readonly List<Character> Dead = new List<Character>();
        private static readonly StringBuilder Text = new StringBuilder();

        private static void Postfix(EnemyHud __instance)
        {
            if (!MonsterConfig.Enabled.Value) return;
            foreach (var pair in __instance.m_huds)
            {
                var c = pair.Key;
                var hud = pair.Value;
                if (c == null || hud == null || hud.m_gui == null || c.GetComponent<MonsterTierTag>() == null) continue;
                var nv = c.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) continue;
                var zdo = nv.GetZDO();

                if (!Bars.TryGetValue(c, out var state)) { state = new BarState(); Bars[c] = state; }
                UpdateTierLine(hud, zdo, state);

                bool active = zdo.GetBool(MonsterCast.ZdoActive);
                int seq = zdo.GetInt(MonsterCast.ZdoSeq);

                if (active && seq != state.Seq)
                {
                    state.Seq = seq;
                    state.LocalStart = Time.time;
                    float dur = zdo.GetFloat(MonsterCast.ZdoDuration, 1f);
                    float ringSeconds = zdo.GetFloat(MonsterCast.ZdoRingSeconds, 0f);
                    Vector3 center = zdo.GetBool(MonsterCast.ZdoRingAtSet) ? zdo.GetVec3(MonsterCast.ZdoRingAt, c.transform.position) : c.transform.position;
                    if (MonsterBrain.TryRingKind(zdo.GetInt(MonsterCast.ZdoRing), out var kind))
                        GroundRingManager.Show(kind, center, zdo.GetFloat(MonsterCast.ZdoRadius, 4f), ringSeconds > 0f ? ringSeconds : dur, 0L, false);
                }

                if (state.Bar == null && hud.m_healthSlow != null && seq > 0) state.Bar = MakeBar(hud);
                if (state.Bar == null) continue;

                if (!active || !hud.m_gui.activeSelf)
                {
                    if (state.Bar.gameObject.activeSelf) state.Bar.gameObject.SetActive(false);
                    continue;
                }
                float duration = Mathf.Max(0.1f, zdo.GetFloat(MonsterCast.ZdoDuration, 1f));
                state.Bar.gameObject.SetActive(true);
                state.Bar.SetColor(zdo.GetBool(MonsterCast.ZdoInterruptible) ? Gold : Grey);
                state.Bar.SetValue(Mathf.Clamp01((Time.time - state.LocalStart) / duration));
            }

            Dead.Clear();
            foreach (var k in Bars.Keys) if (k == null) Dead.Add(k);
            foreach (var k in Dead) Bars.Remove(k);
        }

        private static void UpdateTierLine(EnemyHud.HudData hud, ZDO zdo, BarState state)
        {
            var tier = MonsterTierTag.TierOf(zdo);
            if (tier == MonsterTier.Normal && !AffixRunner.Inspired(zdo) && !AffixRunner.Linked(zdo) && string.IsNullOrEmpty(Bosses.BossDirector.PatternInfo(zdo)))
            {
                if (state.TierLine != null && state.TierLine.gameObject.activeSelf) state.TierLine.gameObject.SetActive(false);
                return;
            }
            if (state.TierLine == null)
            {
                if (hud.m_name == null) return;
                state.TierLine = MakeTierLine(hud);
                if (state.TierLine == null) return;
            }
            string text = TierText(zdo, tier);
            if (text != state.TierText)
            {
                state.TierText = text;
                state.TierLine.text = text;
                state.TierLine.color = MonsterTierInfo.NameplateColor(tier);
            }
            if (!state.TierLine.gameObject.activeSelf) state.TierLine.gameObject.SetActive(true);
        }

        private static string TierText(ZDO zdo, MonsterTier tier)
        {
            Text.Clear();
            if (tier != MonsterTier.Normal) Text.Append(tier);
            if (tier == MonsterTier.Champion)
            {
                string name = zdo.GetString(MonsterTierTag.ZdoChampionName);
                if (!string.IsNullOrEmpty(name)) Text.Append(' ').Append(name);
            }
            foreach (var a in MonsterAffixInfo.Each(MonsterTierTag.AffixesOf(zdo)))
            {
                Text.Append(" · ");
                if (a == MonsterAffix.Enrage && zdo.GetBool(AffixRunner.ZdoEnraged)) Text.Append("Enraged");
                else if (a == MonsterAffix.Stoneskin && AffixRunner.StoneskinCracked(zdo)) Text.Append("Stoneskin (cracked)");
                else if (a == MonsterAffix.Shielded && AffixRunner.ShieldLeft(zdo) >= 0f)
                {
                    float shield = AffixRunner.ShieldLeft(zdo);
                    Text.Append(shield > 0f ? $"Shielded {shield:0}" : "Shielded (broken)");
                }
                else if (a == MonsterAffix.Reflective && AffixRunner.Reflecting(zdo)) Text.Append("Reflecting!");
                else if (a == MonsterAffix.Frenzied && AffixRunner.FrenzyStacks(zdo) > 0) Text.Append($"Frenzied x{AffixRunner.FrenzyStacks(zdo)}");
                else if (a == MonsterAffix.Warded) Text.Append(AffixRunner.TotemOf(zdo) != null ? "Warded (totem)" : "Warded");
                else if (a == MonsterAffix.Elemental) Text.Append(AffixRunner.ElementLabel(zdo));
                else Text.Append(MonsterAffixInfo.Label(a));
            }
            // A Spirit-linked monster's twin carries no affixes of its own but is part of the pair.
            if ((MonsterTierTag.AffixesOf(zdo) & MonsterAffix.SpiritLinked) == 0 && AffixRunner.Linked(zdo))
                Text.Append(Text.Length > 0 ? " · Spirit-linked" : "Spirit-linked");
            if (AffixRunner.Inspired(zdo)) Text.Append(Text.Length > 0 ? " · Inspired" : "Inspired");
            // A boss's healing pattern (Tethered · 3 Heartwood, Channelling heals, Shelled 120, Feeding · 2 coming).
            string pattern = Bosses.BossDirector.PatternInfo(zdo);
            if (!string.IsNullOrEmpty(pattern)) Text.Append(Text.Length > 0 ? " · " : "").Append(pattern);
            return Text.ToString();
        }

        private static GuiBar MakeBar(EnemyHud.HudData hud)
        {
            var src = hud.m_healthSlow.gameObject;
            var copy = Object.Instantiate(src, src.transform.parent);
            copy.name = "FMO_CastBar";
            var rt = copy.GetComponent<RectTransform>();
            var srcRt = src.GetComponent<RectTransform>();
            if (rt != null && srcRt != null) rt.anchoredPosition = srcRt.anchoredPosition + new Vector2(0f, -srcRt.rect.height - 3f);
            var bar = copy.GetComponent<GuiBar>();
            if (bar != null) bar.SetValue(0f);
            copy.SetActive(false);
            return bar;
        }

        // A copy of the name label, smaller, one line above it.
        private static TextMeshProUGUI MakeTierLine(EnemyHud.HudData hud)
        {
            var src = hud.m_name.gameObject;
            var copy = Object.Instantiate(src, src.transform.parent);
            copy.name = "FMO_TierLine";
            var label = copy.GetComponent<TextMeshProUGUI>();
            if (label == null) { Object.Destroy(copy); return null; }
            label.fontSize = hud.m_name.fontSize * TierFontScale;
            var rt = copy.GetComponent<RectTransform>();
            var srcRt = src.GetComponent<RectTransform>();
            if (rt != null && srcRt != null) rt.anchoredPosition = srcRt.anchoredPosition + new Vector2(0f, srcRt.rect.height * TierFontScale + TierLineGap);
            copy.SetActive(false);
            return label;
        }
    }
}
