using HarmonyLib;
using Verse;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Text;
using Verse.AI;

namespace QuestScheduler
{
    // --- 1. 基礎數據層 (確保編譯順序) ---
    public enum SettingsTab { Quests, RaidPresets }

    public class TraitEntry
    {
        public readonly TraitDef def; public readonly int degree; public readonly string label; public readonly string description; public readonly string uniqueKey; public readonly TraitDegreeData data;
        public TraitEntry(TraitDef def, TraitDegreeData d)
        {
            this.def = def; this.degree = d.degree; this.label = d.label.CapitalizeFirst(); this.description = d.description; this.uniqueKey = $"{def.defName}|{d.degree}"; this.data = d;
        }
    }
    public class Dialog_RaidSettings : Window
    {
        private Map map; private Faction faction; private float points;
        private XenotypeDef selectedXenotype;
        private int minAge; private int maxAge;
        private float maleRatio;

        public Dialog_RaidSettings(Map map, Faction faction, float points)
        {
            this.map = map; this.faction = faction; this.points = points;
            this.doCloseX = true; this.forcePause = true; this.absorbInputAroundWindow = true;

            this.selectedXenotype = QuestSchedulerMod.settings.presetXenotype;
            this.minAge = QuestSchedulerMod.settings.presetAgeMin;
            this.maxAge = QuestSchedulerMod.settings.presetAgeMax;
            this.maleRatio = QuestSchedulerMod.settings.presetMaleRatio;
        }

        public override Vector2 InitialSize => new Vector2(400f, 400f);

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium; listing.Label("自定襲擊詳細設定"); Text.Font = GameFont.Small;
            listing.GapLine();
            listing.Label($"目標派系: {faction.Name} ({faction.PlayerRelationKind.GetLabel()})");
            listing.Label($"襲擊點數: {points:F0} P");
            listing.Gap();

            Rect xenoRect = listing.GetRect(30f);
            Widgets.Label(xenoRect.LeftPart(0.3f), "選擇人種:");
            if (Widgets.ButtonText(xenoRect.RightPart(0.65f), selectedXenotype?.LabelCap ?? "隨機/原樣"))
            {
                List<FloatMenuOption> xOpts = new List<FloatMenuOption> { new FloatMenuOption("隨機/原樣", () => selectedXenotype = null) };
                foreach (var x in DefDatabase<XenotypeDef>.AllDefs.OrderBy(d => d.label))
                    xOpts.Add(new FloatMenuOption(x.LabelCap, () => selectedXenotype = x, x.Icon, Color.white));
                Find.WindowStack.Add(new FloatMenu(xOpts));
            }
            listing.Gap(10f);

            listing.Label($"年齡範圍: {minAge} - {maxAge}");
            Rect ageRect = listing.GetRect(28f);
            IntRange ageRange = new IntRange(minAge, maxAge);
            Widgets.IntRange(ageRect, 881, ref ageRange, 14, 120);
            minAge = ageRange.min; maxAge = ageRange.max;
            listing.Gap(10f);

            string genderLabel = maleRatio == 1f ? "強制全男" : (maleRatio == 0f ? "強制全女" : $"男 {maleRatio * 100:F0}% / 女 {(1 - maleRatio) * 100:F0}%");
            listing.Label($"性別生成比例: {genderLabel}");
            maleRatio = listing.Slider(maleRatio, 0f, 1f);

            listing.Gap(25f);
            if (listing.ButtonText("確認並生成襲擊"))
            {
                CustomRaidGenerator.GenerateRaid(map, faction, points, selectedXenotype, minAge, maxAge, maleRatio);
                this.Close();
            }
            listing.End();
        }
    }

    public class ScheduledQuest : IExposable
    {
        public string questDefName;
        public float intervalHours = 1.0f;
        public int nextTick = -1;
        public bool useCustomPoints = true;
        public float customPointsValue = 1000f;
        public void ExposeData()
        {
            Scribe_Values.Look(ref questDefName, "questDefName");
            Scribe_Values.Look(ref intervalHours, "intervalHours", 1.0f);
            Scribe_Values.Look(ref nextTick, "nextTick", -1);
            Scribe_Values.Look(ref useCustomPoints, "useCustomPoints", true);
            Scribe_Values.Look(ref customPointsValue, "customPointsValue", 1000f);
        }
    }

    public class QuestSettings : ModSettings
    {
        public List<ScheduledQuest> activeSchedules = new List<ScheduledQuest>();

        public int presetAgeMin = 18;
        public int presetAgeMax = 60;
        public XenotypeDef presetXenotype = null;
        public float presetMaleRatio = 0.5f;

        public bool globalStripAllRaiders = true;
        public bool globalParalysisMode = true;
        public float paralysisDays = 2.0f;
        public bool forceCleanBackstories = true;

        public int forcedTraitCountMin = 1;
        public int forcedTraitCountMax = 3;
        public List<string> blacklistedTraitKeys = new List<string>();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref presetAgeMin, "presetAgeMin", 18);
            Scribe_Values.Look(ref presetAgeMax, "presetAgeMax", 60);
            Scribe_Values.Look(ref presetMaleRatio, "presetMaleRatio", 0.5f);

            string xenoName = presetXenotype?.defName;
            Scribe_Values.Look(ref xenoName, "presetXenotypeName");
            if (Scribe.mode == LoadSaveMode.LoadingVars && !string.IsNullOrEmpty(xenoName))
            {
                presetXenotype = DefDatabase<XenotypeDef>.GetNamedSilentFail(xenoName);
            }

            Scribe_Values.Look(ref globalStripAllRaiders, "globalStripAllRaiders", true);
            Scribe_Values.Look(ref globalParalysisMode, "globalParalysisMode", true);
            Scribe_Values.Look(ref paralysisDays, "paralysisDays", 2.0f);
            Scribe_Values.Look(ref forceCleanBackstories, "forceCleanBackstories", true);

            Scribe_Values.Look(ref forcedTraitCountMin, "forcedTraitCountMin", 1);
            Scribe_Values.Look(ref forcedTraitCountMax, "forcedTraitCountMax", 3);

            Scribe_Collections.Look(ref blacklistedTraitKeys, "blacklistedTraitKeys", LookMode.Value);
            Scribe_Collections.Look(ref activeSchedules, "activeSchedules", LookMode.Deep);
            if (activeSchedules == null) activeSchedules = new List<ScheduledQuest>();
            if (blacklistedTraitKeys == null) blacklistedTraitKeys = new List<string>();
        }
    }

    // --- 2. 模組主介面 (包含分頁與 UI 選項恢復) ---
    public class QuestSchedulerMod : Mod
    {
        public static QuestSettings settings;
        public static QuestSchedulerMod instance;
        private static SettingsTab currentTab = SettingsTab.Quests;
        private Vector2 scrollPos = Vector2.zero;

        public QuestSchedulerMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<QuestSettings>(); instance = this;
            new Harmony("com.yourname.questscheduler.pro").PatchAll();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect tabRect = new Rect(inRect.x, inRect.y, inRect.width, 40f);
            if (Widgets.ButtonText(new Rect(tabRect.x, tabRect.y, 160f, 35f), "📅 任務排程", currentTab == SettingsTab.Quests)) currentTab = SettingsTab.Quests;
            if (Widgets.ButtonText(new Rect(tabRect.x + 170f, tabRect.y, 160f, 35f), "⚔️ 襲擊預設", currentTab == SettingsTab.RaidPresets)) currentTab = SettingsTab.RaidPresets;

            Rect mainRect = new Rect(inRect.x, inRect.y + 45f, inRect.width, inRect.height - 50f);
            if (currentTab == SettingsTab.Quests) DrawQuestTab(mainRect);
            else DrawPresetTab(mainRect);
        }

        private void DrawQuestTab(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard(); listing.Begin(rect);
            if (Widgets.ButtonText(listing.GetRect(30f).LeftPart(0.15f), "添加任務"))
            {
                List<FloatMenuOption> qOpts = new List<FloatMenuOption>();
                foreach (var q in DefDatabase<QuestScriptDef>.AllDefs.OrderBy(d => d.defName))
                    qOpts.Add(new FloatMenuOption(q.defName, () => settings.activeSchedules.Add(new ScheduledQuest { questDefName = q.defName })));
                Find.WindowStack.Add(new FloatMenu(qOpts));
            }
            listing.GapLine(12f); float listY = listing.CurHeight + 15f; listing.End();

            Rect outRect = new Rect(rect.x, rect.y + listY, rect.width, rect.height - listY - 10f);
            Rect viewRect = new Rect(0, 0, outRect.width - 25f, settings.activeSchedules.Count * 115f);
            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
            float curY = 0;
            for (int i = 0; i < settings.activeSchedules.Count; i++)
            {
                var s = settings.activeSchedules[i];
                Rect row = new Rect(0, curY, viewRect.width, 105f); Widgets.DrawBoxSolidWithOutline(row, new Color(0.15f, 0.15f, 0.15f, 0.6f), Color.gray);
                Widgets.Label(new Rect(row.x + 10, row.y + 10, 300, 25), $"<b>任務: {s.questDefName}</b>");
                if (Widgets.ButtonText(new Rect(row.xMax - 80, row.y + 10, 70, 30), "移除", true, true, Color.red)) { settings.activeSchedules.RemoveAt(i); break; }

                Widgets.Label(new Rect(row.x + 15, row.y + 40, 80, 25), "生成間隔:");
                s.intervalHours = Widgets.HorizontalSlider(new Rect(row.x + 100, row.y + 45, 150, 20), s.intervalHours, 0.1f, 168f, true, s.intervalHours.ToString("F1") + " 小時");

                Widgets.CheckboxLabeled(new Rect(row.x + 280, row.y + 45, 90, 25), "自定點數", ref s.useCustomPoints);
                if (s.useCustomPoints) s.customPointsValue = Widgets.HorizontalSlider(new Rect(row.x + 380, row.y + 50, 150, 20), s.customPointsValue, 100f, 10000f, true, s.customPointsValue.ToString("F0") + " P");
                curY += 115f;
            }
            Widgets.EndScrollView();
        }

        private void DrawPresetTab(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            listing.Label($"預設年齡範圍: {settings.presetAgeMin} - {settings.presetAgeMax}");
            Rect ageRect = listing.GetRect(28f);
            IntRange ageRange = new IntRange(settings.presetAgeMin, settings.presetAgeMax);
            Widgets.IntRange(ageRect, 8881, ref ageRange, 14, 120);
            settings.presetAgeMin = ageRange.min;
            settings.presetAgeMax = ageRange.max;

            listing.Gap(10f);

            string genderLabel = settings.presetMaleRatio == 1f ? "強制全男" : (settings.presetMaleRatio == 0f ? "強制全女" : $"男性 {settings.presetMaleRatio * 100:F0}% / 女性 {(1 - settings.presetMaleRatio) * 100:F0}%");
            listing.Label($"預設性別生成比例: {genderLabel}");
            settings.presetMaleRatio = listing.Slider(settings.presetMaleRatio, 0f, 1f);

            listing.Gap(10f);

            Rect xenoRect = listing.GetRect(30f);
            Widgets.Label(xenoRect.LeftPart(0.3f), "預設人種:");
            if (Widgets.ButtonText(xenoRect.RightPart(0.65f), settings.presetXenotype?.LabelCap ?? "隨機/原樣 (Baseliner)"))
            {
                List<FloatMenuOption> xOptions = new List<FloatMenuOption> { new FloatMenuOption("清除預設 (隨機/原樣)", () => settings.presetXenotype = null) };
                foreach (var x in DefDatabase<XenotypeDef>.AllDefs.OrderBy(d => d.label))
                {
                    xOptions.Add(new FloatMenuOption(x.LabelCap, () => settings.presetXenotype = x, x.Icon, Color.white));
                }
                Find.WindowStack.Add(new FloatMenu(xOptions));
            }

            listing.GapLine();

            listing.CheckboxLabeled("預設純淨背景故事 (無工作懲罰/無負面技能)", ref settings.forceCleanBackstories);

            listing.Gap(10f);

            listing.Label($"預設生成特性數量: {settings.forcedTraitCountMin} - {settings.forcedTraitCountMax} 個");
            Rect traitRect = listing.GetRect(28f);
            IntRange traitRange = new IntRange(settings.forcedTraitCountMin, settings.forcedTraitCountMax);
            Widgets.IntRange(traitRect, 8882, ref traitRange, 1, 5);
            settings.forcedTraitCountMin = traitRange.min;
            settings.forcedTraitCountMax = traitRange.max;

            listing.Gap(5f);

            if (listing.ButtonText("管理禁用特性黑名單 (支援拖曳選擇)..."))
            {
                Find.WindowStack.Add(new Dialog_TraitBlacklist());
            }

            listing.GapLine();

            listing.CheckboxLabeled("全域自動扒光所有裝備與物品", ref settings.globalStripAllRaiders);
            listing.CheckboxLabeled("全域自動麻醉 (癱瘓)", ref settings.globalParalysisMode);
            listing.Label($"預設麻醉天數: {settings.paralysisDays:F1}");
            settings.paralysisDays = listing.Slider(settings.paralysisDays, 1f, 4f);

            listing.End();
        }
        public override string SettingsCategory() => "Quest Scheduler Pro";
    }

    // --- 3. 研究桌右鍵選單 (含一鍵預設召喚) ---
    [HarmonyPatch(typeof(ThingWithComps), "GetFloatMenuOptions")]
    public static class Patch_ResearchMenu
    {
        [HarmonyPostfix]
        public static void Postfix(ThingWithComps __instance, ref IEnumerable<FloatMenuOption> __result)
        {
            if (__instance is Building_ResearchBench || __instance.def.defName.ToLower().Contains("researchbench"))
            {
                var opts = __result?.ToList() ?? new List<FloatMenuOption>();

                opts.Add(new FloatMenuOption("💎 按預設規格呼叫人礦 (一鍵)", () => {
                    List<FloatMenuOption> fOpts = new List<FloatMenuOption>();
                    foreach (var fac in Find.FactionManager.AllFactionsVisible.Where(f => !f.IsPlayer && !f.Hidden))
                    {
                        fOpts.Add(new FloatMenuOption(fac.Name, () => CustomRaidGenerator.GenerateRaid(__instance.Map, fac, 5000f, QuestSchedulerMod.settings.presetXenotype, QuestSchedulerMod.settings.presetAgeMin, QuestSchedulerMod.settings.presetAgeMax, QuestSchedulerMod.settings.presetMaleRatio)));
                    }
                    Find.WindowStack.Add(new FloatMenu(fOpts));
                }));

                opts.Add(new FloatMenuOption("⚔️ 呼叫派系襲擊 (自訂規格)...", () => {
                    List<FloatMenuOption> facOpts = new List<FloatMenuOption>();
                    foreach (var fac in Find.FactionManager.AllFactionsVisible.Where(f => !f.IsPlayer && !f.Hidden))
                    {
                        string label = $"{fac.Name} ({fac.PlayerRelationKind.GetLabel()})";
                        facOpts.Add(new FloatMenuOption(label, () => OpenPointsMenu(__instance.Map, fac)));
                    }
                    Find.WindowStack.Add(new FloatMenu(facOpts));
                }));

                opts.Add(new FloatMenuOption("🐾 呼叫癱瘓獸群...", () => {
                    List<FloatMenuOption> animOpts = new List<FloatMenuOption>();
                    foreach (var a in DefDatabase<PawnKindDef>.AllDefs.Where(k => k.RaceProps.Animal).OrderBy(k => k.label))
                    {
                        animOpts.Add(new FloatMenuOption(a.LabelCap, () => OpenAnimalPointsMenu(__instance.Map, a), a.race.uiIcon, Color.white));
                    }
                    Find.WindowStack.Add(new FloatMenu(animOpts));
                }));

                __result = opts;
            }
        }

        private static void OpenPointsMenu(Map m, Faction f)
        {
            List<FloatMenuOption> pOpts = new List<FloatMenuOption>();
            float[] pts = { 1000f, 5000f, 10000f };
            foreach (float p in pts)
            {
                pOpts.Add(new FloatMenuOption($"{p:F0} 點數", () => Find.WindowStack.Add(new Dialog_RaidSettings(m, f, p))));
            }
            pOpts.Add(new FloatMenuOption("<b>自定義點數...</b>", () => {
                Find.WindowStack.Add(new Dialog_Slider((int val) => $"輸入點數: {val}", 100, 20000, (int val) => {
                    Find.WindowStack.Add(new Dialog_RaidSettings(m, f, (float)val));
                }));
            }));
            Find.WindowStack.Add(new FloatMenu(pOpts));
        }

        private static void OpenAnimalPointsMenu(Map m, PawnKindDef animal)
        {
            List<FloatMenuOption> pOpts = new List<FloatMenuOption>();
            float[] pts = { 1000f, 5000f, 10000f };
            foreach (float p in pts)
            {
                pOpts.Add(new FloatMenuOption($"{p:F0} 點數", () => CustomRaidGenerator.GenerateAnimalRaid(m, animal, p)));
            }
            pOpts.Add(new FloatMenuOption("<b>自定義點數...</b>", () => {
                Find.WindowStack.Add(new Dialog_Slider((int val) => $"輸入點數: {val}", 100, 20000, (int val) => {
                    CustomRaidGenerator.GenerateAnimalRaid(m, animal, (float)val);
                }));
            }));
            Find.WindowStack.Add(new FloatMenu(pOpts));
        }
    }


    // --- 4. 特性黑名單 (Tooltip + 拖曳) ---
    public class Dialog_TraitBlacklist : Window
    {
        private Vector2 scrollPos; private string query = ""; private readonly List<TraitEntry> cachedEntries;
        private bool isDragging = false; private bool dragTargetState = false;

        public Dialog_TraitBlacklist()
        {
            this.doCloseX = true; this.doCloseButton = true; this.absorbInputAroundWindow = true;
            cachedEntries = new List<TraitEntry>();
            foreach (var t in DefDatabase<TraitDef>.AllDefs) foreach (var d in t.degreeDatas) cachedEntries.Add(new TraitEntry(t, d));
            cachedEntries = cachedEntries.OrderBy(e => e.label).ToList();
        }

        public override void DoWindowContents(Rect inRect)
        {
            query = Widgets.TextField(new Rect(0, 0, inRect.width, 30f), query);
            if (Event.current.type == EventType.MouseUp) isDragging = false;
            Rect outRect = new Rect(0, 35f, inRect.width, inRect.height - 80f);
            var filtered = cachedEntries.Where(e => e.label.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            Rect viewRect = new Rect(0, 0, outRect.width - 20f, filtered.Count * 30f);
            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
            float curY = 0f;
            foreach (var entry in filtered)
            {
                Rect row = new Rect(0, curY, viewRect.width, 28f);
                if (Mouse.IsOver(row))
                {
                    if (Event.current.type == EventType.MouseDown) { isDragging = true; dragTargetState = !QuestSchedulerMod.settings.blacklistedTraitKeys.Contains(entry.uniqueKey); }
                    if (isDragging)
                    {
                        if (dragTargetState && !QuestSchedulerMod.settings.blacklistedTraitKeys.Contains(entry.uniqueKey)) QuestSchedulerMod.settings.blacklistedTraitKeys.Add(entry.uniqueKey);
                        else if (!dragTargetState) QuestSchedulerMod.settings.blacklistedTraitKeys.Remove(entry.uniqueKey);
                    }
                }
                Widgets.DrawHighlightIfMouseover(row);
                TooltipHandler.TipRegion(row, new TipSignal(() => $"<b>{entry.label}</b>\n\n{entry.description}{GetEffects(entry.data)}", entry.uniqueKey.GetHashCode()));
                bool isBlocked = QuestSchedulerMod.settings.blacklistedTraitKeys.Contains(entry.uniqueKey);
                Widgets.CheckboxLabeled(row, entry.label, ref isBlocked); curY += 30f;
            }
            Widgets.EndScrollView();
        }
        private string GetEffects(TraitDegreeData d)
        {
            StringBuilder sb = new StringBuilder();
            if (d.statOffsets != null) foreach (var s in d.statOffsets) sb.AppendLine($" - {s.stat.LabelCap}: {s.ValueToStringAsOffset}");
            if (d.statFactors != null) foreach (var s in d.statFactors) sb.AppendLine($" - {s.stat.LabelCap}: x{s.value.ToStringPercent()}");
            if (d.skillGains != null) foreach (var s in d.skillGains) sb.AppendLine($" - {s.skill.LabelCap}: {s.amount.ToStringWithSign()}");
            return sb.Length > 0 ? "\n\n<b>數值影響:</b>\n" + sb.ToString().TrimEnd() : "";
        }
    }

    // --- 5. 生成邏輯與 1.6 API 適配 ---
    public static class CustomRaidGenerator
    {
        public static bool isSpawning = false;
        public static bool isSpawningAnimal = false;
        public static XenotypeDef forcedXeno;
        public static float minAge, maxAge;
        public static float targetMaleRatio = 0.5f;

        public static void GenerateRaid(Map m, Faction f, float p, XenotypeDef x, int min, int max, float mRatio)
        {
            isSpawning = true; forcedXeno = x; minAge = min; maxAge = max; targetMaleRatio = mRatio;
            try
            {
                var kind = f.def.pawnGroupMakers?.SelectMany(gm => gm.options).OrderBy(o => o.kind.combatPower).FirstOrDefault()?.kind ?? DefDatabase<PawnKindDef>.GetNamedSilentFail("Baseliner");
                IncidentParms parms = new IncidentParms { target = m, faction = f, points = p, pawnKind = kind };
                IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
            }
            finally { isSpawning = false; }
        }

        public static void GenerateAnimalRaid(Map m, PawnKindDef a, float p)
        {
            isSpawningAnimal = true;
            try
            {
                IncidentParms parms = new IncidentParms { target = m, pawnKind = a, points = p };
                IncidentDefOf.ManhunterPack.Worker.TryExecute(parms);
            }
            finally { isSpawningAnimal = false; }
        }
    }

    [HarmonyPatch(typeof(PawnGenerator), "GeneratePawn", new[] { typeof(PawnGenerationRequest) })]
    public static class Patch_Generator
    {
        [HarmonyPrefix]
        public static void Prefix(ref PawnGenerationRequest request)
        {
            if (CustomRaidGenerator.isSpawning)
            {
                request.FixedBiologicalAge = Rand.Range(CustomRaidGenerator.minAge, CustomRaidGenerator.maxAge);
                request.AllowedDevelopmentalStages = DevelopmentalStage.Adult;
                if (CustomRaidGenerator.forcedXeno != null) request.ForcedXenotype = CustomRaidGenerator.forcedXeno;
                request.FixedGender = Rand.Value < CustomRaidGenerator.targetMaleRatio ? Gender.Male : Gender.Female;
            }
            else if (CustomRaidGenerator.isSpawningAnimal)
            {
                request.FixedBiologicalAge = Rand.Range(1f, 10f);
            }
        }

        [HarmonyPostfix]
        public static void Postfix(ref Pawn __result)
        {
            if (CustomRaidGenerator.isSpawning && __result?.story != null)
            {
                if (QuestSchedulerMod.settings.forceCleanBackstories)
                {
                    __result.story.Childhood = DefDatabase<BackstoryDef>.AllDefs.Where(b => b.slot == BackstorySlot.Childhood && b.workDisables == WorkTags.None).RandomElementWithFallback();
                    __result.story.Adulthood = DefDatabase<BackstoryDef>.AllDefs.Where(b => b.slot == BackstorySlot.Adulthood && b.workDisables == WorkTags.None).RandomElementWithFallback();
                }

                __result.story.traits.allTraits.Clear();
                var validTraits = new List<TraitEntry>();
                foreach (var t in DefDatabase<TraitDef>.AllDefs)
                {
                    if (t.degreeDatas != null)
                    {
                        foreach (var d in t.degreeDatas)
                        {
                            if (!QuestSchedulerMod.settings.blacklistedTraitKeys.Contains($"{t.defName}|{d.degree}"))
                            {
                                validTraits.Add(new TraitEntry(t, d));
                            }
                        }
                    }
                }

                int targetTraitCount = Rand.RangeInclusive(QuestSchedulerMod.settings.forcedTraitCountMin, QuestSchedulerMod.settings.forcedTraitCountMax);
                int attempts = 0;

                while (__result.story.traits.allTraits.Count < targetTraitCount && validTraits.Any() && attempts < 100)
                {
                    attempts++;
                    var entry = validTraits.RandomElement();
                    if (entry == null) break;

                    Trait newTrait = new Trait(entry.def, entry.degree);
                    if (!__result.story.traits.HasTrait(entry.def) && !__result.story.traits.allTraits.Any(t => t.def.ConflictsWith(newTrait)))
                    {
                        __result.story.traits.GainTrait(newTrait);
                    }
                    validTraits.Remove(entry);
                }
            }
        }
    }

    // ==========================================================
    // --- 6. 核心制裁系統：執行扒光與麻醉邏輯 (取代舊的 Patch_Modifier) ---
    // ==========================================================
    public static class PunishmentUtility
    {
        // 判定危險的精神狀態 (包含殖民者殺戮、動物發狂復仇等)
        public static bool IsDangerousState(MentalStateDef mDef)
        {
            if (mDef == null) return false;
            string name = mDef.defName;

            return name.Contains("Manhunter") || name == "Berserk" || name == "MurderousRage" ||
                   name == "SocialFighting" || name == "Slaughterer" || name == "Jailbreaker" ||
                   name.Contains("Tantrum") || name.Contains("Rebellion") || name.Contains("Breakout");
        }

        // 執行具體動作 (會先檢查玩家設定是否有開啟)
        public static void ExecutePunishment(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned) return;

            // 1. 扒光處理
            if (QuestSchedulerMod.settings.globalStripAllRaiders)
            {
                pawn.equipment?.DestroyAllEquipment();
                pawn.apparel?.DestroyAll();
                pawn.inventory?.innerContainer?.ClearAndDestroyContents();
            }

            // 2. 麻醉處理
            if (QuestSchedulerMod.settings.globalParalysisMode)
            {
                if (pawn.health != null && pawn.health.hediffSet != null)
                {
                    // 精準清除狂亂症(Scaria)與其他負面健康狀態 (延續你昨天的邏輯)
                    var hediffsToRemove = pawn.health.hediffSet.hediffs
                        .Where(h => h.def == HediffDefOf.Scaria || h.def.isBad)
                        .ToList();

                    foreach (var h in hediffsToRemove)
                    {
                        pawn.health.RemoveHediff(h);
                    }

                    // 植入麻醉 (嚴重度與天數掛鉤)
                    if (!pawn.health.hediffSet.HasHediff(HediffDefOf.Anesthetic))
                    {
                        Hediff anes = HediffMaker.MakeHediff(HediffDefOf.Anesthetic, pawn, null);
                        anes.Severity = QuestSchedulerMod.settings.paralysisDays * 2.0f;
                        pawn.health.AddHediff(anes);
                    }
                }
                // 強制重置 AI，使其立刻平靜並倒地
                pawn.mindState?.mentalStateHandler?.Reset();
                pawn.jobs?.StopAll();
            }
        }
    }

    // 攔截點 A：新生物生成 (處理自訂生成、系統襲擊、路過發狂動物)
    [HarmonyPatch(typeof(Pawn), "SpawnSetup")]
    public static class Patch_SpawnSetup
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, bool respawningAfterLoad)
        {
            if (respawningAfterLoad || __instance == null) return;

            bool isCustomSpawn = CustomRaidGenerator.isSpawning || CustomRaidGenerator.isSpawningAnimal;
            bool isHostile = __instance.Faction != null && __instance.Faction.HostileTo(Faction.OfPlayer);
            bool isManhunter = __instance.InAggroMentalState;

            // 如果是玩家的殖民者正常生成，絕對跳過 (除非他一生成就發狂)
            if (__instance.Faction == Faction.OfPlayer && !isManhunter) return;

            // 若為自訂呼叫的人礦/獸群，或系統生成的敵人/發狂動物
            if (isCustomSpawn || isHostile || isManhunter)
            {
                PunishmentUtility.ExecutePunishment(__instance);
            }
        }
    }

    // 攔截點 B：精神狀態改變 (處理殖民者崩潰、野生動物受攻擊後反擊)
    [HarmonyPatch(typeof(MentalStateHandler), "TryStartMentalState")]
    public static class Patch_MentalStateTrigger
    {
        [HarmonyPostfix]
        public static void Postfix(MentalStateHandler __instance, bool __result, Pawn ___pawn, MentalStateDef stateDef)
        {
            // 如果成功進入了我們定義的「危險狀態」
            if (__result && ___pawn != null && PunishmentUtility.IsDangerousState(stateDef))
            {
                PunishmentUtility.ExecutePunishment(___pawn);
            }
        }
    }

    // 攔截點 C：工作分配 (處理野生動物因飢餓而發動「掠食者狩獵」襲擊玩家)
    [HarmonyPatch(typeof(Pawn_JobTracker), "StartJob")]
    public static class Patch_PredatorHuntTrigger
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn_JobTracker __instance, Pawn ___pawn, Job newJob)
        {
            if (___pawn == null || newJob == null || newJob.def == null) return;

            // 如果野生動物發起了「掠食者狩獵」
            if (newJob.def.defName == "PredatorHunt")
            {
                // 檢查牠鎖定的目標是否為玩家派系 (殖民者或馴服的動物)
                if (newJob.targetA.Thing is Pawn target && target.Faction == Faction.OfPlayer)
                {
                    PunishmentUtility.ExecutePunishment(___pawn);
                }
            }
        }
    }

    public class MainButtonWorker_QuestScheduler : MainButtonWorker { public override void Activate() => Find.WindowStack.Add(new Dialog_ModSettings(QuestSchedulerMod.instance)); }
}