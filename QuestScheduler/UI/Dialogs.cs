using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace QuestScheduler
{
    public class Dialog_CustomRewards : Window
    {
        public Dialog_CustomRewards() { this.doCloseX = true; this.doCloseButton = true; this.absorbInputAroundWindow = true; }
        public override Vector2 InitialSize => new Vector2(600f, 450f);
        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard(); list.Begin(inRect);
            Text.Font = GameFont.Medium; list.Label("🎁 設定自製任務獎勵 (最多 6 個)"); Text.Font = GameFont.Small; list.GapLine();
            for (int i = 0; i < 6; i++)
            {
                Rect row = list.GetRect(35f);
                if (i < QuestSchedulerMod.settings.customRewards.Count)
                {
                    var r = QuestSchedulerMod.settings.customRewards[i];
                    string nameLabel = r.type == RewardType.Item ? (DefDatabase<ThingDef>.GetNamedSilentFail(r.thingDefName)?.LabelCap.ToString() ?? "未知物品") : (r.type == RewardType.Goodwill ? $"友好度 ({r.factionDefName})" : $"榮譽 ({r.factionDefName})");
                    Widgets.Label(row.LeftPart(0.4f), $"{(r.type == RewardType.Item ? "📦" : (r.type == RewardType.Goodwill ? "🤝" : "👑"))} {nameLabel}");
                    string buf = r.amount.ToString(); Widgets.TextFieldNumeric(new Rect(row.x + row.width * 0.45f, row.y, 60f, 30f), ref r.amount, ref buf, 1, 9999);
                    r.amount = (int)Widgets.HorizontalSlider(new Rect(row.x + row.width * 0.45f + 70f, row.y + 10f, 100f, 20f), r.amount, 1, 5000);
                    if (Widgets.ButtonText(new Rect(row.xMax - 60f, row.y, 50f, 30f), "移除")) QuestSchedulerMod.settings.customRewards.RemoveAt(i);
                }
                else
                {
                    if (Widgets.ButtonText(row.LeftPart(0.3f), "➕ 加入獎勵..."))
                    {
                        List<FloatMenuOption> opts = new List<FloatMenuOption>();
                        opts.Add(new FloatMenuOption("📦 物品 (按分類)", () => { List<FloatMenuOption> catOpts = new List<FloatMenuOption>(); foreach (var cat in DefDatabase<ThingCategoryDef>.AllDefs.Where(c => c.childThingDefs.Any()).OrderBy(c => c.label)) catOpts.Add(new FloatMenuOption(cat.LabelCap, () => { List<FloatMenuOption> itemOpts = new List<FloatMenuOption>(); foreach (var item in cat.childThingDefs.OrderBy(d => d.label)) itemOpts.Add(new FloatMenuOption(item.LabelCap, () => QuestSchedulerMod.settings.customRewards.Add(new CustomRewardData { type = RewardType.Item, thingDefName = item.defName, amount = 10 }))); Find.WindowStack.Add(new FloatMenu(itemOpts)); })); Find.WindowStack.Add(new FloatMenu(catOpts)); }));
                        opts.Add(new FloatMenuOption("🤝 派系友好度", () => { List<FloatMenuOption> facOpts = new List<FloatMenuOption>(); foreach (var fac in Find.FactionManager.AllFactionsVisible.Where(f => !f.IsPlayer && !f.Hidden)) facOpts.Add(new FloatMenuOption(fac.Name, () => QuestSchedulerMod.settings.customRewards.Add(new CustomRewardData { type = RewardType.Goodwill, factionDefName = fac.def.defName, amount = 20 }))); Find.WindowStack.Add(new FloatMenu(facOpts)); }));
                        opts.Add(new FloatMenuOption("👑 榮譽 (Royal Favor)", () => { List<FloatMenuOption> facOpts = new List<FloatMenuOption>(); foreach (var fac in Find.FactionManager.AllFactionsVisible.Where(f => !f.IsPlayer && !f.Hidden && f.def.HasRoyalTitles)) facOpts.Add(new FloatMenuOption(fac.Name, () => QuestSchedulerMod.settings.customRewards.Add(new CustomRewardData { type = RewardType.Honor, factionDefName = fac.def.defName, amount = 5 }))); if (facOpts.Count == 0) facOpts.Add(new FloatMenuOption("沒有支援榮譽系統的派系", null)); Find.WindowStack.Add(new FloatMenu(facOpts)); }));
                        Find.WindowStack.Add(new FloatMenu(opts));
                    }
                }
                list.Gap(5f);
            }
            list.End();
        }
    }

    public class Dialog_SelectQuest : Window
    {
        private Vector2 scrollPos; private string searchStr = "";
        public Dialog_SelectQuest() { this.doCloseX = true; this.doCloseButton = true; this.absorbInputAroundWindow = true; }
        public override Vector2 InitialSize => new Vector2(400f, 600f);
        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium; Widgets.Label(new Rect(0, 0, inRect.width, 35f), "選擇要生成的任務"); Text.Font = GameFont.Small;
            searchStr = Widgets.TextField(new Rect(0, 40f, inRect.width, 30f), searchStr); Rect outRect = new Rect(0, 80f, inRect.width, inRect.height - 140f);
            var quests = DefDatabase<QuestScriptDef>.AllDefs.Where(q => q.defName.IndexOf(searchStr, System.StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(q => q.defName).ToList();
            Rect viewRect = new Rect(0, 0, outRect.width - 20f, quests.Count * 35f); Widgets.BeginScrollView(outRect, ref scrollPos, viewRect); float curY = 0f;
            foreach (var q in quests)
            {
                if (Widgets.ButtonText(new Rect(0, curY, viewRect.width, 30f), q.defName))
                {
                    QuestSchedulerMod.settings.customQuestDefName = q.defName;
                    string defLow = q.defName.ToLower();
                    QuestSchedulerMod.settings.customQuestRequireSite = (defLow.Contains("site") || defLow.Contains("camp") || defLow.Contains("outpost") || defLow.Contains("stash") || defLow.Contains("village") || defLow.Contains("base"));
                    this.Close();
                }
                curY += 35f;
            }
            Widgets.EndScrollView();
        }
    }

    public class Dialog_RaidSettings : Window
    {
        private Map map; private Faction faction; private float points; private XenotypeDef selectedXenotype; private int minAge; private int maxAge; private float maleRatio;
        public Dialog_RaidSettings(Map map, Faction faction, float points) { this.map = map; this.faction = faction; this.points = points; this.doCloseX = true; this.forcePause = true; this.absorbInputAroundWindow = true; this.selectedXenotype = QuestSchedulerMod.settings.presetXenotype; this.minAge = QuestSchedulerMod.settings.presetAgeMin; this.maxAge = QuestSchedulerMod.settings.presetAgeMax; this.maleRatio = QuestSchedulerMod.settings.presetMaleRatio; }
        public override Vector2 InitialSize => new Vector2(400f, 480f); // 稍微加高以容納新佈局
        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard(); listing.Begin(inRect);
            Text.Font = GameFont.Medium; listing.Label("自定襲擊詳細設定"); Text.Font = GameFont.Small; listing.GapLine();
            listing.Label($"目標派系: {faction.Name} ({faction.PlayerRelationKind.GetLabel()})");

            listing.Label($"襲擊點數: {points:F0} P");
            Rect ptsRect = listing.GetRect(30f);
            string ptsBuf = points.ToString("F0");
            // 左側 30% 輸入數字，右側 65% 拖動滑桿
            Widgets.TextFieldNumeric(ptsRect.LeftPart(0.3f), ref points, ref ptsBuf, 100f, 100000f);
            points = Widgets.HorizontalSlider(ptsRect.RightPart(0.65f), points, 100f, 20000f);

            listing.Gap(10f);
            Rect xenoRect = listing.GetRect(30f); Widgets.Label(xenoRect.LeftPart(0.3f), "選擇人種:");
            if (Widgets.ButtonText(xenoRect.RightPart(0.65f), selectedXenotype?.LabelCap ?? "隨機/原樣")) { List<FloatMenuOption> xOpts = new List<FloatMenuOption> { new FloatMenuOption("隨機/原樣", () => selectedXenotype = null) }; foreach (var x in DefDatabase<XenotypeDef>.AllDefs.OrderBy(d => d.label)) xOpts.Add(new FloatMenuOption(x.LabelCap, () => selectedXenotype = x, x.Icon, Color.white)); Find.WindowStack.Add(new FloatMenu(xOpts)); }
            listing.Gap(10f); listing.Label($"年齡範圍: {minAge} - {maxAge}"); Rect ageRect = listing.GetRect(28f); IntRange ageRange = new IntRange(minAge, maxAge); Widgets.IntRange(ageRect, 881, ref ageRange, 14, 120); minAge = ageRange.min; maxAge = ageRange.max; listing.Gap(10f);
            string genderLabel = maleRatio == 1f ? "強制全男" : (maleRatio == 0f ? "強制全女" : $"男 {maleRatio * 100:F0}% / 女 {(1 - maleRatio) * 100:F0}%"); listing.Label($"性別生成比例: {genderLabel}"); maleRatio = listing.Slider(maleRatio, 0f, 1f); listing.Gap(25f);
            if (listing.ButtonText("確認並生成襲擊")) { CustomRaidGenerator.GenerateRaid(map, faction, points, selectedXenotype, minAge, maxAge, maleRatio); this.Close(); }
            listing.End();
        }
    }

    public class Dialog_AnimalRaidSettings : Window
    {
        private Map map;
        private PawnKindDef animalKind;
        private float points;

        public Dialog_AnimalRaidSettings(Map map, PawnKindDef animalKind, float initialPoints)
        {
            this.map = map;
            this.animalKind = animalKind;
            this.points = initialPoints;
            this.doCloseX = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(400f, 260f);

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
            listing.Label("自定義癱瘓獸群設定");
            Text.Font = GameFont.Small;
            listing.GapLine();

            listing.Label($"目標生物: {animalKind.LabelCap}");
            listing.Gap(10f);

            listing.Label($"襲擊點數: {points:F0} P");
            Rect ptsRect = listing.GetRect(30f);
            string ptsBuf = points.ToString("F0");
            // 同樣採用 數字輸入 + 滑桿 佈局
            Widgets.TextFieldNumeric(ptsRect.LeftPart(0.3f), ref points, ref ptsBuf, 100f, 50000f);
            points = Widgets.HorizontalSlider(ptsRect.RightPart(0.65f), points, 100f, 20000f);

            listing.Gap(25f);

            if (listing.ButtonText("確認並生成獸群"))
            {
                CustomRaidGenerator.GenerateAnimalRaid(map, animalKind, points);
                this.Close();
            }

            listing.End();
        }
    }


    public class Dialog_TraitBlacklist : Window
    {
        private Vector2 scrollPos; private string query = ""; private readonly List<TraitEntry> cachedEntries; private bool isDragging = false; private bool dragTargetState = false;
        public Dialog_TraitBlacklist() { this.doCloseX = true; this.doCloseButton = true; this.absorbInputAroundWindow = true; cachedEntries = new List<TraitEntry>(); foreach (var t in DefDatabase<TraitDef>.AllDefs) foreach (var d in t.degreeDatas) cachedEntries.Add(new TraitEntry(t, d)); cachedEntries = cachedEntries.OrderBy(e => e.label).ToList(); }
        public override void DoWindowContents(Rect inRect) { query = Widgets.TextField(new Rect(0, 0, inRect.width, 30f), query); if (Event.current.type == EventType.MouseUp) isDragging = false; Rect outRect = new Rect(0, 35f, inRect.width, inRect.height - 80f); var filtered = cachedEntries.Where(e => e.label.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList(); Rect viewRect = new Rect(0, 0, outRect.width - 20f, filtered.Count * 30f); Widgets.BeginScrollView(outRect, ref scrollPos, viewRect); float curY = 0f; foreach (var entry in filtered) { Rect row = new Rect(0, curY, viewRect.width, 28f); if (Mouse.IsOver(row)) { if (Event.current.type == EventType.MouseDown) { isDragging = true; dragTargetState = !QuestSchedulerMod.settings.blacklistedTraitKeys.Contains(entry.uniqueKey); } if (isDragging) { if (dragTargetState && !QuestSchedulerMod.settings.blacklistedTraitKeys.Contains(entry.uniqueKey)) QuestSchedulerMod.settings.blacklistedTraitKeys.Add(entry.uniqueKey); else if (!dragTargetState) QuestSchedulerMod.settings.blacklistedTraitKeys.Remove(entry.uniqueKey); } } Widgets.DrawHighlightIfMouseover(row); TooltipHandler.TipRegion(row, new TipSignal(() => $"<b>{entry.label}</b>\n\n{entry.description}{GetEffects(entry.data)}", entry.uniqueKey.GetHashCode())); bool isBlocked = QuestSchedulerMod.settings.blacklistedTraitKeys.Contains(entry.uniqueKey); Widgets.CheckboxLabeled(row, entry.label, ref isBlocked); curY += 30f; } Widgets.EndScrollView(); }
        private string GetEffects(TraitDegreeData d) { StringBuilder sb = new StringBuilder(); if (d.statOffsets != null) foreach (var s in d.statOffsets) sb.AppendLine($" - {s.stat.LabelCap}: {s.ValueToStringAsOffset}"); if (d.statFactors != null) foreach (var s in d.statFactors) sb.AppendLine($" - {s.stat.LabelCap}: x{s.value.ToStringPercent()}"); if (d.skillGains != null) foreach (var s in d.skillGains) sb.AppendLine($" - {s.skill.LabelCap}: {s.amount.ToStringWithSign()}"); return sb.Length > 0 ? "\n\n<b>數值影響:</b>\n" + sb.ToString().TrimEnd() : ""; }
    }
}