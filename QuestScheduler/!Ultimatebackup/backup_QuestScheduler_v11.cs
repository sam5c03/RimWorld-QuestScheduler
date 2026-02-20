using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;

namespace QuestScheduler
{
    // --- 1. 基礎數據與自訂獎勵資料層 ---
    public enum SettingsTab { Quests, RaidPresets, CustomQuests }
    public enum RewardType { Item, Goodwill, Honor }

    public class TraitEntry
    {
        public readonly TraitDef def; public readonly int degree; public readonly string label; public readonly string description; public readonly string uniqueKey; public readonly TraitDegreeData data;
        public TraitEntry(TraitDef def, TraitDegreeData d)
        {
            this.def = def; this.degree = d.degree; this.label = d.label.CapitalizeFirst(); this.description = d.description; this.uniqueKey = $"{def.defName}|{d.degree}"; this.data = d;
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

    public class CustomRewardData : IExposable
    {
        public RewardType type;
        public string thingDefName;
        public int amount = 1;
        public string factionDefName;

        public void ExposeData()
        {
            Scribe_Values.Look(ref type, "type");
            Scribe_Values.Look(ref thingDefName, "thingDefName");
            Scribe_Values.Look(ref amount, "amount", 1);
            Scribe_Values.Look(ref factionDefName, "factionDefName");
        }
    }

    public class QuestSchedulerSettings : ModSettings
    {
        public List<ScheduledQuest> activeSchedules = new List<ScheduledQuest>();

        // 襲擊預設
        public int presetAgeMin = 18;
        public int presetAgeMax = 60;
        public XenotypeDef presetXenotype = null;
        public float presetMaleRatio = 0.5f;

        // 自製任務全域預設存檔
        public string customQuestDefName = "OpportunitySite_BanditCamp";
        public float customQuestPoints = 1000f;
        public int customQuestAgeMin = 18;
        public int customQuestAgeMax = 60;
        public float customQuestFemaleRatio = 50f;
        public int customQuestTraitsMin = 1;
        public int customQuestTraitsMax = 3;
        // 【新增】自製任務人種與人數
        public XenotypeDef customQuestXenotype = null;
        public int customQuestPawnCount = -1;
        public bool customQuestRequireSite = true;

        // 自訂獎勵存檔與開關
        public bool useCustomRewards = false;
        public List<CustomRewardData> customRewards = new List<CustomRewardData>();
        // 【新增】記錄自製任務的地塊，以便抵達時強制套用人種
        public List<int> customSiteTiles = new List<int>();

        // 全域制裁
        public bool stripEnemies = true;
        public bool stripAnimals = false;
        public bool paralysisEnemies = true;
        public bool paralysisAnimals = false;
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

            Scribe_Values.Look(ref customQuestDefName, "customQuestDefName", "OpportunitySite_BanditCamp");
            Scribe_Values.Look(ref customQuestPoints, "customQuestPoints", 1000f);
            Scribe_Values.Look(ref customQuestAgeMin, "customQuestAgeMin", 18);
            Scribe_Values.Look(ref customQuestAgeMax, "customQuestAgeMax", 60);
            Scribe_Values.Look(ref customQuestFemaleRatio, "customQuestFemaleRatio", 50f);
            Scribe_Values.Look(ref customQuestTraitsMin, "customQuestTraitsMin", 1);
            Scribe_Values.Look(ref customQuestTraitsMax, "customQuestTraitsMax", 3);
            Scribe_Values.Look(ref customQuestRequireSite, "customQuestRequireSite", true);
            Scribe_Values.Look(ref useCustomRewards, "useCustomRewards", false);
            // 【新增】自製任務人種與人數存檔
            Scribe_Values.Look(ref customQuestPawnCount, "customQuestPawnCount", -1);
            string cqXenoName = customQuestXenotype?.defName;
            Scribe_Values.Look(ref cqXenoName, "customQuestXenotypeName");
            if (Scribe.mode == LoadSaveMode.LoadingVars && !string.IsNullOrEmpty(cqXenoName))
                customQuestXenotype = DefDatabase<XenotypeDef>.GetNamedSilentFail(cqXenoName);

            string xenoName = presetXenotype?.defName;
            Scribe_Values.Look(ref xenoName, "presetXenotypeName");
            if (Scribe.mode == LoadSaveMode.LoadingVars && !string.IsNullOrEmpty(xenoName))
                presetXenotype = DefDatabase<XenotypeDef>.GetNamedSilentFail(xenoName);

            Scribe_Values.Look(ref stripEnemies, "stripEnemies", true);
            Scribe_Values.Look(ref stripAnimals, "stripAnimals", false);
            Scribe_Values.Look(ref paralysisEnemies, "paralysisEnemies", true);
            Scribe_Values.Look(ref paralysisAnimals, "paralysisAnimals", false);
            Scribe_Values.Look(ref paralysisDays, "paralysisDays", 2.0f);
            Scribe_Values.Look(ref forceCleanBackstories, "forceCleanBackstories", true);
            Scribe_Values.Look(ref forcedTraitCountMin, "forcedTraitCountMin", 1);
            Scribe_Values.Look(ref forcedTraitCountMax, "forcedTraitCountMax", 3);

            Scribe_Collections.Look(ref blacklistedTraitKeys, "blacklistedTraitKeys", LookMode.Value);
            Scribe_Collections.Look(ref activeSchedules, "activeSchedules", LookMode.Deep);
            Scribe_Collections.Look(ref customRewards, "customRewards", LookMode.Deep);
            // 【新增】地塊存檔
            Scribe_Collections.Look(ref customSiteTiles, "customSiteTiles", LookMode.Value);
            if (customSiteTiles == null) customSiteTiles = new List<int>();

            if (activeSchedules == null) activeSchedules = new List<ScheduledQuest>();
            if (blacklistedTraitKeys == null) blacklistedTraitKeys = new List<string>();
            if (customRewards == null) customRewards = new List<CustomRewardData>();
        }
    }

    // --- 2. 模組主介面 ---
    public class QuestSchedulerMod : Mod
    {
        public static QuestSchedulerSettings settings;
        public static QuestSchedulerMod instance;
        private static SettingsTab currentTab = SettingsTab.Quests;
        private Vector2 scrollPos = Vector2.zero;
        public static Faction customQuestFaction = null;

        // 【新增修復：存檔重置保護】
        // 這個 Patch 會在每次玩家載入存檔、回到主選單或切換存檔時執行
        [HarmonyPatch(typeof(Game), "LoadGame")]
        public static class Patch_ResetStaleReferences
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                // 當存檔載入完成後，強制清空靜態引用
                // 這樣 UI 就會重新顯示「系統預設 (推薦)」，而不是指著舊世界的損壞派系
                QuestSchedulerMod.customQuestFaction = null;
                Log.Message("[QuestScheduler] 偵測到存檔載入，已自動重置派系引用以防止關係損壞。");
            }
        }

        // 同樣地，在離開存檔回到主選單時也建議重置
        [HarmonyPatch(typeof(GenScene), "GoToMainMenu")]
        public static class Patch_ResetOnExit
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                QuestSchedulerMod.customQuestFaction = null;
            }
        }

        public QuestSchedulerMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<QuestSchedulerSettings>(); instance = this;
            new Harmony("com.questschedulermod.main").PatchAll();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect tabRect = new Rect(inRect.x, inRect.y, inRect.width, 40f);
            if (Widgets.ButtonText(new Rect(tabRect.x, tabRect.y, 140f, 35f), "📅 任務排程", currentTab == SettingsTab.Quests)) currentTab = SettingsTab.Quests;
            if (Widgets.ButtonText(new Rect(tabRect.x + 150f, tabRect.y, 140f, 35f), "⚔️ 襲擊預設", currentTab == SettingsTab.RaidPresets)) currentTab = SettingsTab.RaidPresets;
            if (Widgets.ButtonText(new Rect(tabRect.x + 300f, tabRect.y, 140f, 35f), "📜 自製任務", currentTab == SettingsTab.CustomQuests)) currentTab = SettingsTab.CustomQuests;

            Rect mainRect = new Rect(inRect.x, inRect.y + 45f, inRect.width, inRect.height - 50f);

            if (currentTab == SettingsTab.Quests) DrawQuestTab(mainRect);
            else if (currentTab == SettingsTab.RaidPresets) DrawPresetTab(mainRect);
            else DrawCustomQuestTab(mainRect);
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
            Listing_Standard listing = new Listing_Standard(); listing.Begin(rect);
            listing.Label($"預設年齡範圍: {settings.presetAgeMin} - {settings.presetAgeMax}");
            Rect ageRect = listing.GetRect(28f); IntRange ageRange = new IntRange(settings.presetAgeMin, settings.presetAgeMax); Widgets.IntRange(ageRect, 8881, ref ageRange, 14, 120);
            settings.presetAgeMin = ageRange.min; settings.presetAgeMax = ageRange.max; listing.Gap(10f);

            string genderLabel = settings.presetMaleRatio == 1f ? "強制全男" : (settings.presetMaleRatio == 0f ? "強制全女" : $"男性 {settings.presetMaleRatio * 100:F0}% / 女性 {(1 - settings.presetMaleRatio) * 100:F0}%");
            listing.Label($"預設性別生成比例: {genderLabel}"); settings.presetMaleRatio = listing.Slider(settings.presetMaleRatio, 0f, 1f); listing.Gap(10f);

            Rect xenoRect = listing.GetRect(30f); Widgets.Label(xenoRect.LeftPart(0.3f), "預設人種:");
            if (Widgets.ButtonText(xenoRect.RightPart(0.65f), settings.presetXenotype?.LabelCap ?? "隨機/原樣 (Baseliner)"))
            {
                List<FloatMenuOption> xOptions = new List<FloatMenuOption> { new FloatMenuOption("清除預設 (隨機/原樣)", () => settings.presetXenotype = null) };
                foreach (var x in DefDatabase<XenotypeDef>.AllDefs.OrderBy(d => d.label)) xOptions.Add(new FloatMenuOption(x.LabelCap, () => settings.presetXenotype = x, x.Icon, Color.white));
                Find.WindowStack.Add(new FloatMenu(xOptions));
            }
            listing.GapLine(); listing.CheckboxLabeled("預設純淨背景故事 (無工作懲罰/無負面技能)", ref settings.forceCleanBackstories); listing.Gap(10f);
            listing.Label($"預設生成特性數量: {settings.forcedTraitCountMin} - {settings.forcedTraitCountMax} 個");
            Rect traitRect = listing.GetRect(28f); IntRange traitRange = new IntRange(settings.forcedTraitCountMin, settings.forcedTraitCountMax); Widgets.IntRange(traitRect, 8882, ref traitRange, 1, 5);
            settings.forcedTraitCountMin = traitRange.min; settings.forcedTraitCountMax = traitRange.max; listing.Gap(5f);
            if (listing.ButtonText("管理禁用特性黑名單 (支援拖曳選擇)...")) Find.WindowStack.Add(new Dialog_TraitBlacklist());
            listing.GapLine();
            listing.Label("🛡️ 全域扒光設定 (武器、服裝、物品)"); listing.CheckboxLabeled("  - 針對 敵對人類/發狂殖民者", ref settings.stripEnemies); listing.CheckboxLabeled("  - 針對 發狂/飢餓野生動物", ref settings.stripAnimals); listing.Gap(5f);
            listing.Label("💉 全域麻醉設定 (癱瘓)"); listing.CheckboxLabeled("  - 針對 敵對人類/發狂殖民者", ref settings.paralysisEnemies); listing.CheckboxLabeled("  - 針對 發狂/飢餓野生動物", ref settings.paralysisAnimals);
            listing.Gap(5f); listing.Label($"預設麻醉天數: {settings.paralysisDays:F1}"); settings.paralysisDays = listing.Slider(settings.paralysisDays, 1f, 4f);
            listing.End();
        }

        private Vector2 customQuestScrollPos = Vector2.zero;

        // 【新增】智能判定：該任務是否為需要生成角色的「複雜任務」？
        public static bool IsComplexQuest(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return false;
            string low = defName.ToLower();

            // 法令、難民、乞丐、動物、開局任務、商隊等，視為簡單任務
            if (low.Contains("decree") || low.Contains("trade") || low.Contains("hospitality") ||
                low.Contains("beggar") || low.Contains("refugee") || low.Contains("animal") ||
                low.Contains("wanderer") || low.Contains("intro") || low.Contains("chased") ||
                low.Contains("relic") || low.Contains("roamer") || low.Contains("ship") ||
                low.Contains("pods") || low.Contains("rescue"))
            {
                return false;
            }
            return true;
        }

        private void DrawCustomQuestTab(Rect rect)
        {
            Rect outRect = new Rect(rect.x, rect.y, rect.width, rect.height);
            Rect viewRect = new Rect(0, 0, outRect.width - 20f, 900f); // 加大內部高度以容納新選項

            // 確保 BeginScrollView 有被呼叫
            Widgets.BeginScrollView(outRect, ref customQuestScrollPos, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            Text.Font = GameFont.Medium;
            listing.Label("🛠️ 建立自製任務 (全域保存)");
            Text.Font = GameFont.Small;
            listing.GapLine();

            Rect questRect = listing.GetRect(30f);
            Widgets.Label(questRect.LeftPart(0.2f), "1. 選擇任務:");
            if (Widgets.ButtonText(questRect.RightPart(0.75f), settings.customQuestDefName))
                Find.WindowStack.Add(new Dialog_SelectQuest());
            listing.Gap(10f);

            bool isComplex = IsComplexQuest(settings.customQuestDefName);
            if (isComplex)
            {
                Rect facRect = listing.GetRect(30f);
                Widgets.Label(facRect.LeftPart(0.2f), "2. 目標派系:");

                // 【安全檢查】檢查目前選中的派系是否屬於當前運作中的世界
                if (customQuestFaction != null)
                {
                    // 如果派系不在當前世界的派系管理器中，說明它是來自舊存檔的殘留，直接清空
                    if (!Find.FactionManager.AllFactions.Contains(customQuestFaction))
                    {
                        customQuestFaction = null;
                    }
                }

                string facLabel = "系統預設 (推薦)";
                if (customQuestFaction != null)
                {
                    // 再次確認關係是否存在
                    if (customQuestFaction.RelationWith(Faction.OfPlayer, true) != null)
                        facLabel = $"{customQuestFaction.Name} [{customQuestFaction.PlayerRelationKind.GetLabel()}]";
                    else
                        facLabel = $"{customQuestFaction.Name} [關係數據遺失]";
                }

                if (Widgets.ButtonText(facRect.RightPart(0.75f), facLabel))
                {
                    List<FloatMenuOption> facOpts = new List<FloatMenuOption> { new FloatMenuOption("系統預設 (推薦)", () => customQuestFaction = null) };

                    // 只列出目前世界中健康、可見、且有關係數據的派系
                    var validFactions = Find.FactionManager.AllFactionsVisible
                        .Where(f => !f.IsPlayer && !f.Hidden && f.RelationWith(Faction.OfPlayer, true) != null);

                    foreach (var f in validFactions)
                    {
                        facOpts.Add(new FloatMenuOption($"{f.Name} [{f.PlayerRelationKind.GetLabel()}] ({f.PlayerGoodwill})", () => customQuestFaction = f));
                    }
                    Find.WindowStack.Add(new FloatMenu(facOpts));
                }
                listing.Gap(10f);

                listing.Label("3. 規模點數:");
                Rect ptsRect = listing.GetRect(30f);
                string ptsBuf = settings.customQuestPoints.ToString("F0");
                Widgets.TextFieldNumeric(ptsRect.LeftPart(0.2f), ref settings.customQuestPoints, ref ptsBuf, 0f, 100000f);
                settings.customQuestPoints = Widgets.HorizontalSlider(ptsRect.RightPart(0.75f), settings.customQuestPoints, 0f, 20000f);
                listing.Gap(10f);

                // 【新增】生成人數
                listing.Label("4. 生成人數 (輸入 -1 代表由系統預設決定):");
                Rect countRect = listing.GetRect(30f);
                string countBuf = settings.customQuestPawnCount.ToString();
                Widgets.TextFieldNumeric(countRect.LeftPart(0.2f), ref settings.customQuestPawnCount, ref countBuf, -1, 200);
                settings.customQuestPawnCount = (int)Widgets.HorizontalSlider(countRect.RightPart(0.75f), settings.customQuestPawnCount, -1f, 200f);
                listing.Gap(10f);

                // 【新增】人種選擇
                Rect cxenoRect = listing.GetRect(30f);
                Widgets.Label(cxenoRect.LeftPart(0.2f), "5. 人種選擇:");
                if (Widgets.ButtonText(cxenoRect.RightPart(0.75f), settings.customQuestXenotype?.LabelCap ?? "系統預設 (不介入生成)"))
                {
                    List<FloatMenuOption> xOptions = new List<FloatMenuOption> { new FloatMenuOption("系統預設 (不介入生成)", () => settings.customQuestXenotype = null) };
                    foreach (var x in DefDatabase<XenotypeDef>.AllDefs.OrderBy(d => d.label))
                        xOptions.Add(new FloatMenuOption(x.LabelCap, () => settings.customQuestXenotype = x, x.Icon, Color.white));
                    Find.WindowStack.Add(new FloatMenu(xOptions));
                }
                listing.Gap(10f);

                listing.Label("6. 生成角色年齡範圍:");
                Rect ageRect = listing.GetRect(30f);
                string minAgeBuf = settings.customQuestAgeMin.ToString();
                string maxAgeBuf = settings.customQuestAgeMax.ToString();
                Widgets.TextFieldNumeric(ageRect.LeftPart(0.1f), ref settings.customQuestAgeMin, ref minAgeBuf, 14, 120);
                Widgets.Label(new Rect(ageRect.x + ageRect.width * 0.12f, ageRect.y, 20f, 30f), "-");
                Widgets.TextFieldNumeric(new Rect(ageRect.x + ageRect.width * 0.15f, ageRect.y, ageRect.width * 0.1f, 30f), ref settings.customQuestAgeMax, ref maxAgeBuf, 14, 120);
                IntRange ageRange = new IntRange(settings.customQuestAgeMin, settings.customQuestAgeMax);
                Widgets.IntRange(ageRect.RightPart(0.7f), 9991, ref ageRange, 14, 120);
                settings.customQuestAgeMin = ageRange.min; settings.customQuestAgeMax = ageRange.max;
                listing.Gap(10f);

                listing.Label("7. 性別比例:");
                Rect genRect = listing.GetRect(30f);
                string ratioBuf = settings.customQuestFemaleRatio.ToString("F0");
                Widgets.TextFieldNumeric(genRect.LeftPart(0.1f), ref settings.customQuestFemaleRatio, ref ratioBuf, 0f, 100f);
                Rect sliderArea = new Rect(genRect.x + genRect.width * 0.15f, genRect.y, genRect.width * 0.6f, 30f);
                Widgets.Label(new Rect(sliderArea.x, sliderArea.y, 50f, 30f), "只有男");
                settings.customQuestFemaleRatio = Widgets.HorizontalSlider(new Rect(sliderArea.x + 50f, sliderArea.y + 8f, sliderArea.width - 100f, 20f), settings.customQuestFemaleRatio, 0f, 100f);
                Widgets.Label(new Rect(sliderArea.xMax - 50f, sliderArea.y, 50f, 30f), "只有女");
                if (Widgets.ButtonText(genRect.RightPart(0.2f), "快速選擇 ▼"))
                {
                    List<FloatMenuOption> opts = new List<FloatMenuOption> {
                        new FloatMenuOption("只有男 (0%)", () => settings.customQuestFemaleRatio = 0f),
                        new FloatMenuOption("只有女 (100%)", () => settings.customQuestFemaleRatio = 100f)
                    };
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
                listing.Gap(10f);

                listing.Label("8. 生成特性數量 (1-5):");
                Rect traitRect = listing.GetRect(30f);
                string minTraitBuf = settings.customQuestTraitsMin.ToString();
                string maxTraitBuf = settings.customQuestTraitsMax.ToString();
                Widgets.TextFieldNumeric(traitRect.LeftPart(0.1f), ref settings.customQuestTraitsMin, ref minTraitBuf, 1, 5);
                Widgets.Label(new Rect(traitRect.x + traitRect.width * 0.12f, traitRect.y, 20f, 30f), "-");
                Widgets.TextFieldNumeric(new Rect(traitRect.x + traitRect.width * 0.15f, traitRect.y, traitRect.width * 0.1f, 30f), ref settings.customQuestTraitsMax, ref maxTraitBuf, 1, 5);
                IntRange tRange = new IntRange(settings.customQuestTraitsMin, settings.customQuestTraitsMax);
                Widgets.IntRange(traitRect.RightPart(0.7f), 9992, ref tRange, 1, 5);
                settings.customQuestTraitsMin = tRange.min; settings.customQuestTraitsMax = tRange.max;
                listing.Gap(15f);

                listing.CheckboxLabeled("9. 🎁 啟用自訂任務獎勵", ref settings.useCustomRewards, "開啟後將替換系統獎品。若任務不支援會彈出警告，請關閉後再生成。");
                if (settings.useCustomRewards)
                {
                    listing.Label($"   (目前設定: {settings.customRewards.Count}/6)");
                    if (Widgets.ButtonText(listing.GetRect(35f).LeftPart(0.4f), "點此設定自訂獎勵清單..."))
                    {
                        Find.WindowStack.Add(new Dialog_CustomRewards());
                    }
                }
                listing.Gap(15f);

                listing.CheckboxLabeled("10. 🗺️ 在世界地圖上手動選擇生成地點", ref settings.customQuestRequireSite, "勾選後，按下生成會跳至世界地圖讓你點擊位置生成。");
                listing.Gap(15f);
            }
            else
            {
                listing.Gap(20f);
                listing.Label("<color=grey>💡 此任務為事件型或簡單任務 (如法令、動物、流浪者等)。\n進階自訂參數已自動隱藏，系統將以原版預設設定為您安全生成。</color>");
                listing.Gap(20f);
            }

            Rect btnRect = listing.GetRect(50f);
            Text.Font = GameFont.Medium;
            if (Widgets.ButtonText(btnRect, "🚀 立即生成任務"))
            {
                if (isComplex && settings.customQuestRequireSite)
                {
                    Find.WindowStack.TryRemove(typeof(Dialog_ModSettings));
                    Find.WorldSelector.ClearSelection();
                    Find.World.renderer.wantedMode = WorldRenderMode.Planet;

                    Find.WorldTargeter.BeginTargeting(new System.Func<GlobalTargetInfo, bool>(target =>
                    {
                        if (target.IsValid && target.Tile >= 0)
                        {
                            GenerateQuestFinal(target.Tile);
                        }
                        return true;
                    }), true);
                }
                else
                {
                    GenerateQuestFinal(-1);
                }
            }
            Text.Font = GameFont.Small;

            // 【最重要的地方】：絕對不能漏掉這兩行結束宣告！
            listing.End();
            Widgets.EndScrollView();
        }

        public static void GenerateQuestFinal(int tileIndex)
        {
            QuestScriptDef qDef = DefDatabase<QuestScriptDef>.GetNamedSilentFail(settings.customQuestDefName);
            if (qDef == null) return;

            bool isComplex = IsComplexQuest(settings.customQuestDefName);
            Slate slate = new Slate();

            // 【修復】補足 Grammar 變數，防止 1.6 文本紅字
            slate.Set("discoveryMethod", "發現了");

            if (isComplex)
            {
                CustomRaidGenerator.minAge = settings.customQuestAgeMin;
                CustomRaidGenerator.maxAge = settings.customQuestAgeMax;
                CustomRaidGenerator.targetFemaleRatio = settings.customQuestFemaleRatio;
                settings.forcedTraitCountMin = settings.customQuestTraitsMin;
                settings.forcedTraitCountMax = settings.customQuestTraitsMax;
                CustomRaidGenerator.forcedXeno = settings.customQuestXenotype; // 加入人種變數
                CustomRaidGenerator.isSpawningCustomQuest = true;

                slate.Set("points", settings.customQuestPoints);
                if (customQuestFaction != null) slate.Set("faction", customQuestFaction);

                // 【注入】人數控制變數
                if (settings.customQuestPawnCount != -1)
                {
                    slate.Set("pawnCount", settings.customQuestPawnCount);
                    slate.Set("enemyCount", settings.customQuestPawnCount);
                    slate.Set("population", settings.customQuestPawnCount);
                    slate.Set("sitePawnCount", settings.customQuestPawnCount);
                }

                Log.Message($"[QuestScheduler] 正在生成任務: {qDef.defName}, 設定人數: {settings.customQuestPawnCount}, 人種: {(settings.customQuestXenotype?.label ?? "預設")}");
            }
            else
            {
                CustomRaidGenerator.isSpawningCustomQuest = false;
            }

            try
            {
                Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(qDef, slate);
                if (quest != null)
                {
                    // 【還原 V9】若有指定地塊，傳送據點
                    if (tileIndex >= 0)
                    {
                        foreach (var part in quest.PartsListForReading.OfType<QuestPart_SpawnWorldObject>())
                        {
                            if (part.worldObject != null)
                            {
                                part.worldObject.Tile = tileIndex;
                            }
                        }
                    }

                    // 【注入】精準記錄地塊座標，用於隨機生成時的攔截
                    foreach (var part in quest.PartsListForReading.OfType<QuestPart_SpawnWorldObject>())
                    {
                        if (part.worldObject != null && part.worldObject.Tile >= 0)
                        {
                            if (!settings.customSiteTiles.Contains(part.worldObject.Tile))
                                settings.customSiteTiles.Add(part.worldObject.Tile);
                            Log.Message($"[QuestScheduler] 據點已記憶！真實地塊座標: {part.worldObject.Tile}");
                        }
                    }

                    // 【還原 V9】自訂獎勵覆寫邏輯
                    if (isComplex && settings.useCustomRewards && settings.customRewards.Count > 0)
                    {
                        var choicePart = quest.PartsListForReading.OfType<QuestPart_Choice>().FirstOrDefault();
                        var endSuccessPart = quest.PartsListForReading.OfType<QuestPart_QuestEnd>().FirstOrDefault(p => p.outcome == QuestEndOutcome.Success);

                        if (choicePart == null || endSuccessPart == null)
                        {
                            Find.QuestManager.Remove(quest);
                            Find.WindowStack.Add(new Dialog_MessageBox("⚠️ 偵測到自訂獎勵與系統衝突！\n\n您選擇的任務不支援標準的獎勵選擇結構，強制替換會導致系統崩潰。\n\n請在設定中【關閉自訂任務獎勵】以使用系統預設獎品池，或選擇其他類型的任務。", "了解並返回"));
                            return;
                        }

                        string successSignal = endSuccessPart.inSignal;
                        choicePart.choices.Clear();
                        QuestPart_Choice.Choice newChoice = new QuestPart_Choice.Choice();
                        List<Thing> itemsToDrop = new List<Thing>();

                        foreach (var r in settings.customRewards)
                        {
                            if (r.type == RewardType.Item)
                            {
                                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(r.thingDefName);
                                if (def != null)
                                {
                                    Thing t = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
                                    t.stackCount = r.amount;
                                    itemsToDrop.Add(t);
                                }
                            }
                            else if (r.type == RewardType.Goodwill)
                            {
                                Faction fac = Find.FactionManager.AllFactionsVisible.FirstOrDefault(f => f.def.defName == r.factionDefName);
                                if (fac != null)
                                {
                                    Reward_Goodwill rw = new Reward_Goodwill();
                                    rw.faction = fac; rw.amount = r.amount;
                                    newChoice.rewards.Add(rw);

                                    QuestPart_FactionGoodwillChange gwPart = new QuestPart_FactionGoodwillChange();
                                    gwPart.faction = fac; gwPart.change = r.amount; gwPart.inSignal = successSignal;
                                    quest.AddPart(gwPart);
                                }
                            }
                            else if (r.type == RewardType.Honor)
                            {
                                Faction fac = Find.FactionManager.AllFactionsVisible.FirstOrDefault(f => f.def.defName == r.factionDefName);
                                if (fac != null)
                                {
                                    Reward_RoyalFavor rw = new Reward_RoyalFavor();
                                    rw.faction = fac; rw.amount = r.amount;
                                    newChoice.rewards.Add(rw);

                                    QuestPart_GiveRoyalFavor honorPart = new QuestPart_GiveRoyalFavor();
                                    honorPart.faction = fac; honorPart.amount = r.amount; honorPart.inSignal = successSignal; honorPart.giveToAccepter = true;
                                    quest.AddPart(honorPart);
                                }
                            }
                        }

                        if (itemsToDrop.Count > 0)
                        {
                            Reward_Items rwItem = new Reward_Items();
                            rwItem.items.AddRange(itemsToDrop);
                            newChoice.rewards.Add(rwItem);

                            QuestPart_DropPods dropPods = new QuestPart_DropPods();
                            dropPods.Things = itemsToDrop; dropPods.inSignal = successSignal; dropPods.mapParent = Find.AnyPlayerHomeMap?.Parent;
                            quest.AddPart(dropPods);
                        }

                        choicePart.choices.Add(newChoice);
                        var partsToRemove = quest.PartsListForReading.Where(p => (p is QuestPart_DropPods && p != quest.PartsListForReading.Last()) || p is QuestPart_GiveRoyalFavor || p is QuestPart_FactionGoodwillChange).ToList();
                        foreach (var p in partsToRemove) quest.RemovePart(p);
                    }

                    // 【還原 V9】信件發送邏輯
                    string defNameLow = qDef.defName.ToLower();
                    if (defNameLow.Contains("site") || defNameLow.Contains("camp") || defNameLow.Contains("outpost"))
                    {
                        if (quest.State == QuestState.NotYetAccepted)
                        {
                            foreach (var choicePart in quest.PartsListForReading.OfType<QuestPart_Choice>().ToList())
                            {
                                if (choicePart.choices != null && choicePart.choices.Count > 0) choicePart.Choose(choicePart.choices[0]);
                            }
                            quest.Accept(null);
                            Find.LetterStack.ReceiveLetter("自製任務已觸發: " + quest.name, "據點已在地圖上生成！", LetterDefOf.PositiveEvent, null, null, quest);
                        }
                    }
                    else
                    {
                        QuestUtility.SendLetterQuestAvailable(quest);
                    }
                    Messages.Message($"成功生成任務: {quest.name}", MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    // 【還原 V9】失敗提示
                    Messages.Message("任務生成失敗，可能是因為地塊限制 (例如離家太近) 或派系不符合條件。", MessageTypeDefOf.RejectInput);
                }
            }
            finally
            {
                CustomRaidGenerator.isSpawningCustomQuest = false;
            }
        }
        public override string SettingsCategory() => "Quest Scheduler Pro";
    }

    // --- 2.5 右鍵研究桌生成選單 ---
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

        public override Vector2 InitialSize => new Vector2(400f, 450f);

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            Text.Font = GameFont.Medium; listing.Label("自定襲擊詳細設定"); Text.Font = GameFont.Small;
            listing.GapLine();
            listing.Label($"目標派系: {faction.Name} ({faction.PlayerRelationKind.GetLabel()})");

            listing.Label($"襲擊點數: {points:F0} P");
            points = listing.Slider(points, 100f, 20000f);
            listing.Gap(10f);

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

    [HarmonyPatch(typeof(ThingWithComps), "GetFloatMenuOptions")]
    public static class Patch_ResearchMenu
    {
        [HarmonyPostfix]
        public static void Postfix(ThingWithComps __instance, ref IEnumerable<FloatMenuOption> __result)
        {
            if (__instance is Building_ResearchBench || __instance.def.defName.ToLower().Contains("researchbench"))
            {
                var opts = __result?.ToList() ?? new List<FloatMenuOption>();

                opts.Add(new FloatMenuOption("💎 按預設規格呼叫人礦 (一鍵)", () =>
                {
                    List<FloatMenuOption> fOpts = new List<FloatMenuOption>();
                    foreach (var fac in Find.FactionManager.AllFactionsVisible.Where(f => !f.IsPlayer && !f.Hidden))
                    {
                        fOpts.Add(new FloatMenuOption(fac.Name, () => CustomRaidGenerator.GenerateRaid(__instance.Map, fac, 5000f, QuestSchedulerMod.settings.presetXenotype, QuestSchedulerMod.settings.presetAgeMin, QuestSchedulerMod.settings.presetAgeMax, QuestSchedulerMod.settings.presetMaleRatio)));
                    }
                    Find.WindowStack.Add(new FloatMenu(fOpts));
                }));

                opts.Add(new FloatMenuOption("⚔️ 呼叫派系襲擊 (自訂規格)...", () =>
                {
                    List<FloatMenuOption> facOpts = new List<FloatMenuOption>();
                    foreach (var fac in Find.FactionManager.AllFactionsVisible.Where(f => !f.IsPlayer && !f.Hidden))
                    {
                        string label = $"{fac.Name} ({fac.PlayerRelationKind.GetLabel()})";
                        facOpts.Add(new FloatMenuOption(label, () => OpenPointsMenu(__instance.Map, fac)));
                    }
                    Find.WindowStack.Add(new FloatMenu(facOpts));
                }));

                opts.Add(new FloatMenuOption("🐾 呼叫癱瘓獸群...", () =>
                {
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
            pOpts.Add(new FloatMenuOption("<b>自定義點數...</b>", () =>
            {
                Find.WindowStack.Add(new Dialog_RaidSettings(m, f, 3000f));
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
            pOpts.Add(new FloatMenuOption("<b>自定義點數...</b>", () =>
            {
                CustomRaidGenerator.GenerateAnimalRaid(m, animal, 3000f);
            }));
            Find.WindowStack.Add(new FloatMenu(pOpts));
        }
    }

    // --- 3. 自訂任務與獎勵介面 ---
    public class Dialog_CustomRewards : Window
    {
        public Dialog_CustomRewards() { this.doCloseX = true; this.doCloseButton = true; this.absorbInputAroundWindow = true; }
        public override Vector2 InitialSize => new Vector2(600f, 450f);

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);

            Text.Font = GameFont.Medium;
            list.Label("🎁 設定自製任務獎勵 (最多 6 個)");
            Text.Font = GameFont.Small;
            list.GapLine();

            for (int i = 0; i < 6; i++)
            {
                Rect row = list.GetRect(35f);
                if (i < QuestSchedulerMod.settings.customRewards.Count)
                {
                    var r = QuestSchedulerMod.settings.customRewards[i];

                    string nameLabel;
                    if (r.type == RewardType.Item)
                    {
                        var def = DefDatabase<ThingDef>.GetNamedSilentFail(r.thingDefName);
                        nameLabel = def != null ? def.LabelCap.ToString() : "未知物品";
                    }
                    else if (r.type == RewardType.Goodwill)
                        nameLabel = $"友好度 ({r.factionDefName})";
                    else
                        nameLabel = $"榮譽 ({r.factionDefName})";

                    Widgets.Label(row.LeftPart(0.4f), $"{(r.type == RewardType.Item ? "📦" : (r.type == RewardType.Goodwill ? "🤝" : "👑"))} {nameLabel}");

                    string buf = r.amount.ToString();
                    Widgets.TextFieldNumeric(new Rect(row.x + row.width * 0.45f, row.y, 60f, 30f), ref r.amount, ref buf, 1, 9999);
                    r.amount = (int)Widgets.HorizontalSlider(new Rect(row.x + row.width * 0.45f + 70f, row.y + 10f, 100f, 20f), r.amount, 1, 5000);

                    if (Widgets.ButtonText(new Rect(row.xMax - 60f, row.y, 50f, 30f), "移除"))
                    {
                        QuestSchedulerMod.settings.customRewards.RemoveAt(i);
                    }
                }
                else
                {
                    if (Widgets.ButtonText(row.LeftPart(0.3f), "➕ 加入獎勵..."))
                    {
                        List<FloatMenuOption> opts = new List<FloatMenuOption>();

                        opts.Add(new FloatMenuOption("📦 物品 (按分類)", () =>
                        {
                            List<FloatMenuOption> catOpts = new List<FloatMenuOption>();
                            foreach (var cat in DefDatabase<ThingCategoryDef>.AllDefs.Where(c => c.childThingDefs.Any()).OrderBy(c => c.label))
                            {
                                catOpts.Add(new FloatMenuOption(cat.LabelCap, () =>
                                {
                                    List<FloatMenuOption> itemOpts = new List<FloatMenuOption>();
                                    foreach (var item in cat.childThingDefs.OrderBy(d => d.label))
                                    {
                                        itemOpts.Add(new FloatMenuOption(item.LabelCap, () =>
                                        {
                                            QuestSchedulerMod.settings.customRewards.Add(new CustomRewardData { type = RewardType.Item, thingDefName = item.defName, amount = 10 });
                                        }));
                                    }
                                    Find.WindowStack.Add(new FloatMenu(itemOpts));
                                }));
                            }
                            Find.WindowStack.Add(new FloatMenu(catOpts));
                        }));

                        opts.Add(new FloatMenuOption("🤝 派系友好度", () =>
                        {
                            List<FloatMenuOption> facOpts = new List<FloatMenuOption>();
                            foreach (var fac in Find.FactionManager.AllFactionsVisible.Where(f => !f.IsPlayer && !f.Hidden))
                            {
                                facOpts.Add(new FloatMenuOption(fac.Name, () =>
                                {
                                    QuestSchedulerMod.settings.customRewards.Add(new CustomRewardData { type = RewardType.Goodwill, factionDefName = fac.def.defName, amount = 20 });
                                }));
                            }
                            Find.WindowStack.Add(new FloatMenu(facOpts));
                        }));

                        opts.Add(new FloatMenuOption("👑 榮譽 (Royal Favor)", () =>
                        {
                            List<FloatMenuOption> facOpts = new List<FloatMenuOption>();
                            foreach (var fac in Find.FactionManager.AllFactionsVisible.Where(f => !f.IsPlayer && !f.Hidden && f.def.HasRoyalTitles))
                            {
                                facOpts.Add(new FloatMenuOption(fac.Name, () =>
                                {
                                    QuestSchedulerMod.settings.customRewards.Add(new CustomRewardData { type = RewardType.Honor, factionDefName = fac.def.defName, amount = 5 });
                                }));
                            }
                            if (facOpts.Count == 0) facOpts.Add(new FloatMenuOption("沒有支援榮譽系統的派系", null));
                            Find.WindowStack.Add(new FloatMenu(facOpts));
                        }));

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
        private Vector2 scrollPos;
        private string searchStr = "";
        public Dialog_SelectQuest() { this.doCloseX = true; this.doCloseButton = true; this.absorbInputAroundWindow = true; }
        public override Vector2 InitialSize => new Vector2(400f, 600f);
        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium; Widgets.Label(new Rect(0, 0, inRect.width, 35f), "選擇要生成的任務"); Text.Font = GameFont.Small;
            searchStr = Widgets.TextField(new Rect(0, 40f, inRect.width, 30f), searchStr);
            Rect outRect = new Rect(0, 80f, inRect.width, inRect.height - 140f);
            var quests = DefDatabase<QuestScriptDef>.AllDefs.Where(q => q.defName.IndexOf(searchStr, System.StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(q => q.defName).ToList();
            Rect viewRect = new Rect(0, 0, outRect.width - 20f, quests.Count * 35f);
            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
            float curY = 0f;
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

    // --- 4. 生成邏輯與攔截 ---
    public static class CustomRaidGenerator
    {
        public static bool isSpawning = false;
        public static bool isSpawningAnimal = false;
        public static bool isSpawningCustomQuest = false;
        public static XenotypeDef forcedXeno;
        public static float minAge, maxAge;
        public static float targetFemaleRatio = 50f;
        public static float targetMaleRatio = 0.5f;

        public static void GenerateRaid(Map m, Faction f, float p, XenotypeDef x, int min, int max, float mRatio)
        {
            isSpawning = true; forcedXeno = x; minAge = min; maxAge = max; targetMaleRatio = mRatio; targetFemaleRatio = (1f - mRatio) * 100f;
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
        public static bool IsCustomSiteGeneration(int requestTile = -1)
        {
            if (CustomRaidGenerator.isSpawningCustomQuest) return true;
            int currentMapTile = Verse.MapGenerator.mapBeingGenerated?.Tile ?? -1;
            bool isCustom = (requestTile >= 0 && QuestSchedulerMod.settings.customSiteTiles.Contains(requestTile)) ||
                            (currentMapTile >= 0 && QuestSchedulerMod.settings.customSiteTiles.Contains(currentMapTile));
            return isCustom;
        }

        [HarmonyPrefix]
        public static void Prefix(ref PawnGenerationRequest request)
        {
            if (IsCustomSiteGeneration(request.Tile))
            {
                if (!request.FixedBiologicalAge.HasValue)
                    request.FixedBiologicalAge = Rand.Range(QuestSchedulerMod.settings.customQuestAgeMin, QuestSchedulerMod.settings.customQuestAgeMax);

                request.AllowedDevelopmentalStages = DevelopmentalStage.Adult;
                if (QuestSchedulerMod.settings.customQuestXenotype != null) request.ForcedXenotype = QuestSchedulerMod.settings.customQuestXenotype;

                if (!request.FixedGender.HasValue)
                    request.FixedGender = Rand.Value < (QuestSchedulerMod.settings.customQuestFemaleRatio / 100f) ? Gender.Female : Gender.Male;
            }
            else if (CustomRaidGenerator.isSpawningAnimal && !request.FixedBiologicalAge.HasValue)
            {
                request.FixedBiologicalAge = Rand.Range(1f, 10f); // 獸群年齡設定
            }
        }

        [HarmonyPostfix]
        public static void Postfix(ref Pawn __result, PawnGenerationRequest request)
        {
            if (IsCustomSiteGeneration(request.Tile) && __result?.story != null)
            {
                if (request.KindDef != null && request.KindDef.factionLeader) return;

                // 還原 V9 特性與背景邏輯
                if (QuestSchedulerMod.settings.forceCleanBackstories)
                {
                    __result.story.Childhood = DefDatabase<BackstoryDef>.AllDefs.Where(b => b.slot == BackstorySlot.Childhood && b.workDisables == WorkTags.None).RandomElementWithFallback();
                    __result.story.Adulthood = DefDatabase<BackstoryDef>.AllDefs.Where(b => b.slot == BackstorySlot.Adulthood && b.workDisables == WorkTags.None).RandomElementWithFallback();
                }

                __result.story.traits.allTraits.Clear();
                var valid = DefDatabase<TraitDef>.AllDefs.SelectMany(t => t.degreeDatas.Select(d => new TraitEntry(t, d))).Where(e => !QuestSchedulerMod.settings.blacklistedTraitKeys.Contains(e.uniqueKey)).ToList();
                int count = Rand.RangeInclusive(QuestSchedulerMod.settings.forcedTraitCountMin, QuestSchedulerMod.settings.forcedTraitCountMax);
                for (int i = 0; i < count && valid.Any(); i++)
                {
                    var e = valid.RandomElement();
                    __result.story.traits.GainTrait(new Trait(e.def, e.degree));
                    valid.RemoveAll(x => x.def == e.def);
                }
            }
        }
    }

    // 【新增】暴力人數控制攔截器
    // 【修正：解決 targetCount 不存在與 allowViolence 參數錯誤】
    [HarmonyPatch(typeof(PawnGroupMakerUtility), "GeneratePawns", new[] { typeof(PawnGroupMakerParms), typeof(bool) })]
    public static class Patch_ForcePawnCount
    {
        [HarmonyPostfix]
        public static void Postfix(ref IEnumerable<Pawn> __result, PawnGroupMakerParms parms)
        {
            // 檢查是否為自製任務地塊
            if (parms.tile >= 0 && QuestSchedulerMod.settings.customSiteTiles.Contains(parms.tile) && QuestSchedulerMod.settings.customQuestPawnCount > 0)
            {
                if (parms.faction != null && parms.faction.HostileTo(Faction.OfPlayer))
                {
                    List<Pawn> vanillaPawns = __result.ToList();

                    // 【修正 1】：統一變數名稱為 targetCount
                    int targetCount = QuestSchedulerMod.settings.customQuestPawnCount;

                    Log.Message($"[QuestScheduler] 執行攔截：正在將 {vanillaPawns.Count} 名原版敵人替換為 {targetCount} 名自訂敵人。");

                    // 1. 移除原版生成的單位
                    foreach (var p in vanillaPawns)
                    {
                        Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.Discard);
                    }

                    // 2. 捏造全新單位
                    List<Pawn> newPawns = new List<Pawn>();
                    PawnKindDef genericKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Mercenary_Gunner") ?? PawnKindDefOf.SpaceRefugee;

                    for (int i = 0; i < targetCount; i++)
                    {
                        // 【修正 2】：使用 1.6 標準的小寫名稱 mustBeCapableOfViolence
                        // 去掉 allowViolence，改用 1.6 官方支持的正確多載參數
                        PawnGenerationRequest req = new PawnGenerationRequest(
                            genericKind,
                            parms.faction,
                            context: PawnGenerationContext.NonPlayer,
                            tile: parms.tile,
                            forceGenerateNewPawn: true,
                            mustBeCapableOfViolence: true // 注意：首字母 m 必須小寫
                        );

                        // 3. 逐一賦值（最穩定的寫法，避開構造函數版本差異）
                        req.FixedGender = Rand.Value < (QuestSchedulerMod.settings.customQuestFemaleRatio / 100f) ? Gender.Female : Gender.Male;
                        req.FixedBiologicalAge = Rand.Range(QuestSchedulerMod.settings.customQuestAgeMin, QuestSchedulerMod.settings.customQuestAgeMax);

                        if (QuestSchedulerMod.settings.customQuestXenotype != null)
                        {
                            req.ForcedXenotype = QuestSchedulerMod.settings.customQuestXenotype;
                        }

                        // 生成並加入結果
                        Pawn customPawn = PawnGenerator.GeneratePawn(req);
                        newPawns.Add(customPawn);
                    }

                    // 覆蓋系統結果
                    __result = newPawns;
                    Log.Message($"[QuestScheduler] 替換流程完成。");
                }
            }
        }
    }

    // --- 5. 制裁系統攔截 ---
    public static class PunishmentUtility
    {
        public static bool IsDangerousState(MentalStateDef mDef)
        {
            if (mDef == null) return false;
            string name = mDef.defName;
            return name.Contains("Manhunter") || name == "Berserk" || name == "MurderousRage" ||
                   name == "SocialFighting" || name == "Slaughterer" || name == "Jailbreaker" ||
                   name.Contains("Tantrum") || name.Contains("Rebellion") || name.Contains("Breakout");
        }

        public static void ExecutePunishment(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned) return;

            // --- 🤖 【核心過濾邏輯】防止異象 (Anomaly) 非自然生物損壞 ---

            // 1. 排除突變體 (包含 Shambler, Ghoul 等)
            if (pawn.mutant != null) return;

            // 2. 排除機械族
            if (pawn.RaceProps != null && pawn.RaceProps.IsMechanoid) return;

            // 3. 排除異象實體 (改用安全的字串檢查，避免 FleshTypeDefOf 報錯)
            if (pawn.RaceProps != null && pawn.RaceProps.FleshType != null &&
                pawn.RaceProps.FleshType.defName.Contains("Entity")) return;

            // ------------------------------------------------

            bool isAnimal = pawn.RaceProps != null && pawn.RaceProps.Animal;
            bool shouldStrip = (isAnimal && QuestSchedulerMod.settings.stripAnimals) || (!isAnimal && QuestSchedulerMod.settings.stripEnemies);
            bool shouldParalyze = (isAnimal && QuestSchedulerMod.settings.paralysisAnimals) || (!isAnimal && QuestSchedulerMod.settings.paralysisEnemies);

            if (shouldStrip)
            {
                pawn.equipment?.DestroyAllEquipment();
                pawn.apparel?.DestroyAll();
                pawn.inventory?.innerContainer?.ClearAndDestroyContents();
            }

            if (shouldParalyze)
            {
                if (pawn.health != null && pawn.health.hediffSet != null)
                {
                    // 【修正】改用傳統 List 迴圈，徹底解決 Lambda 轉換報錯的問題
                    List<Hediff> hediffsToRemove = new List<Hediff>();
                    foreach (var h in pawn.health.hediffSet.hediffs)
                    {
                        if (h.def == HediffDefOf.Scaria)
                        {
                            hediffsToRemove.Add(h);
                        }
                        else if (h.def.isBad)
                        {
                            string defNameLow = h.def.defName.ToLower();
                            // 排除維持生物生命的核心負面狀態
                            if (!defNameLow.Contains("shambler") &&
                                !defNameLow.Contains("ghoul") &&
                                !defNameLow.Contains("mutant") &&
                                !defNameLow.Contains("entity"))
                            {
                                hediffsToRemove.Add(h);
                            }
                        }
                    }

                    foreach (var h in hediffsToRemove)
                    {
                        pawn.health.RemoveHediff(h);
                    }

                    if (!pawn.health.hediffSet.HasHediff(HediffDefOf.Anesthetic))
                    {
                        Hediff anes = HediffMaker.MakeHediff(HediffDefOf.Anesthetic, pawn, null);
                        anes.Severity = QuestSchedulerMod.settings.paralysisDays * 2.0f;
                        pawn.health.AddHediff(anes);
                    }
                }
                pawn.mindState?.mentalStateHandler?.Reset();
                pawn.jobs?.StopAll();
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), "SpawnSetup")] public static class Patch_SpawnSetup { [HarmonyPostfix] public static void Postfix(Pawn __instance, bool respawningAfterLoad) { if (respawningAfterLoad || __instance == null) return; bool isCustomSpawn = CustomRaidGenerator.isSpawning || CustomRaidGenerator.isSpawningAnimal || CustomRaidGenerator.isSpawningCustomQuest; bool isHostile = __instance.Faction != null && __instance.Faction.HostileTo(Faction.OfPlayer); bool isManhunter = __instance.InAggroMentalState; if (__instance.Faction == Faction.OfPlayer && !isManhunter) return; if (isCustomSpawn || isHostile || isManhunter) PunishmentUtility.ExecutePunishment(__instance); } }
    [HarmonyPatch(typeof(MentalStateHandler), "TryStartMentalState")] public static class Patch_MentalStateTrigger { [HarmonyPostfix] public static void Postfix(MentalStateHandler __instance, bool __result, Pawn ___pawn, MentalStateDef stateDef) { if (__result && ___pawn != null && PunishmentUtility.IsDangerousState(stateDef)) PunishmentUtility.ExecutePunishment(___pawn); } }
    [HarmonyPatch(typeof(Pawn_JobTracker), "StartJob")] public static class Patch_PredatorHuntTrigger { [HarmonyPostfix] public static void Postfix(Pawn_JobTracker __instance, Pawn ___pawn, Job newJob) { if (___pawn == null || newJob == null || newJob.def == null) return; if (newJob.def.defName == "PredatorHunt" && newJob.targetA.Thing is Pawn target && target.Faction == Faction.OfPlayer) PunishmentUtility.ExecutePunishment(___pawn); } }
    public class MainButtonWorker_QuestScheduler : MainButtonWorker { public override void Activate() => Find.WindowStack.Add(new Dialog_ModSettings(QuestSchedulerMod.instance)); }
    public class Dialog_TraitBlacklist : Window
    {
        private Vector2 scrollPos; private string query = ""; private readonly List<TraitEntry> cachedEntries; private bool isDragging = false; private bool dragTargetState = false;
        public Dialog_TraitBlacklist() { this.doCloseX = true; this.doCloseButton = true; this.absorbInputAroundWindow = true; cachedEntries = new List<TraitEntry>(); foreach (var t in DefDatabase<TraitDef>.AllDefs) foreach (var d in t.degreeDatas) cachedEntries.Add(new TraitEntry(t, d)); cachedEntries = cachedEntries.OrderBy(e => e.label).ToList(); }
        public override void DoWindowContents(Rect inRect) { query = Widgets.TextField(new Rect(0, 0, inRect.width, 30f), query); if (Event.current.type == EventType.MouseUp) isDragging = false; Rect outRect = new Rect(0, 35f, inRect.width, inRect.height - 80f); var filtered = cachedEntries.Where(e => e.label.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList(); Rect viewRect = new Rect(0, 0, outRect.width - 20f, filtered.Count * 30f); Widgets.BeginScrollView(outRect, ref scrollPos, viewRect); float curY = 0f; foreach (var entry in filtered) { Rect row = new Rect(0, curY, viewRect.width, 28f); if (Mouse.IsOver(row)) { if (Event.current.type == EventType.MouseDown) { isDragging = true; dragTargetState = !QuestSchedulerMod.settings.blacklistedTraitKeys.Contains(entry.uniqueKey); } if (isDragging) { if (dragTargetState && !QuestSchedulerMod.settings.blacklistedTraitKeys.Contains(entry.uniqueKey)) QuestSchedulerMod.settings.blacklistedTraitKeys.Add(entry.uniqueKey); else if (!dragTargetState) QuestSchedulerMod.settings.blacklistedTraitKeys.Remove(entry.uniqueKey); } } Widgets.DrawHighlightIfMouseover(row); TooltipHandler.TipRegion(row, new TipSignal(() => $"<b>{entry.label}</b>\n\n{entry.description}{GetEffects(entry.data)}", entry.uniqueKey.GetHashCode())); bool isBlocked = QuestSchedulerMod.settings.blacklistedTraitKeys.Contains(entry.uniqueKey); Widgets.CheckboxLabeled(row, entry.label, ref isBlocked); curY += 30f; } Widgets.EndScrollView(); }
        private string GetEffects(TraitDegreeData d) { StringBuilder sb = new StringBuilder(); if (d.statOffsets != null) foreach (var s in d.statOffsets) sb.AppendLine($" - {s.stat.LabelCap}: {s.ValueToStringAsOffset}"); if (d.statFactors != null) foreach (var s in d.statFactors) sb.AppendLine($" - {s.stat.LabelCap}: x{s.value.ToStringPercent()}"); if (d.skillGains != null) foreach (var s in d.skillGains) sb.AppendLine($" - {s.skill.LabelCap}: {s.amount.ToStringWithSign()}"); return sb.Length > 0 ? "\n\n<b>數值影響:</b>\n" + sb.ToString().TrimEnd() : ""; }
    }
}