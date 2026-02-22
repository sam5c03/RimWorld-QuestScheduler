using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace QuestScheduler
{
    public static class SettingsUI
    {
        private static SettingsTab currentTab = SettingsTab.Quests;
        private static Vector2 scrollPos = Vector2.zero;
        private static Vector2 customQuestScrollPos = Vector2.zero;
        private static string cqPointsBuf;
        private static string cqPawnCountBuf;
        private static string cqAgeMinBuf;
        private static string cqAgeMaxBuf;
        private static string cqTraitsMinBuf;
        private static string cqTraitsMaxBuf;

        public static void DoSettingsWindowContents(Rect inRect)
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

        private static void DrawQuestTab(Rect rect)
        {
            var settings = QuestSchedulerMod.settings;
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

        private static void DrawPresetTab(Rect rect)
        {
            var settings = QuestSchedulerMod.settings;
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

        private static void DrawCustomQuestTab(Rect rect)
        {
            var settings = QuestSchedulerMod.settings;
            Rect outRect = new Rect(rect.x, rect.y, rect.width, rect.height);
            Rect viewRect = new Rect(0, 0, outRect.width - 20f, 900f);

            Widgets.BeginScrollView(outRect, ref customQuestScrollPos, viewRect);
            Listing_Standard listing = new Listing_Standard(); listing.Begin(viewRect);

            Text.Font = GameFont.Medium; listing.Label("🛠️ 建立自製任務 (全域保存)"); Text.Font = GameFont.Small; listing.GapLine();

            Rect questRect = listing.GetRect(30f); Widgets.Label(questRect.LeftPart(0.2f), "1. 選擇任務:");
            if (Widgets.ButtonText(questRect.RightPart(0.75f), settings.customQuestDefName)) Find.WindowStack.Add(new Dialog_SelectQuest());
            listing.Gap(10f);

            bool isComplex = QuestGenerator.IsComplexQuest(settings.customQuestDefName);
            if (isComplex)
            {
                Rect facRect = listing.GetRect(30f); Widgets.Label(facRect.LeftPart(0.2f), "2. 目標派系:");

                if (QuestSchedulerMod.customQuestFaction != null && !Find.FactionManager.AllFactions.Contains(QuestSchedulerMod.customQuestFaction))
                    QuestSchedulerMod.customQuestFaction = null;

                string facLabel = "系統預設 (推薦)";
                if (QuestSchedulerMod.customQuestFaction != null)
                {
                    if (QuestSchedulerMod.customQuestFaction.RelationWith(Faction.OfPlayer, true) != null)
                        facLabel = $"{QuestSchedulerMod.customQuestFaction.Name} [{QuestSchedulerMod.customQuestFaction.PlayerRelationKind.GetLabel()}]";
                    else
                        facLabel = $"{QuestSchedulerMod.customQuestFaction.Name} [關係數據遺失]";
                }

                if (Widgets.ButtonText(facRect.RightPart(0.75f), facLabel))
                {
                    List<FloatMenuOption> facOpts = new List<FloatMenuOption> { new FloatMenuOption("系統預設 (推薦)", () => QuestSchedulerMod.customQuestFaction = null) };
                    var validFactions = Find.FactionManager.AllFactionsVisible.Where(f => !f.IsPlayer && !f.Hidden && f.RelationWith(Faction.OfPlayer, true) != null);
                    foreach (var f in validFactions) facOpts.Add(new FloatMenuOption($"{f.Name} [{f.PlayerRelationKind.GetLabel()}] ({f.PlayerGoodwill})", () => QuestSchedulerMod.customQuestFaction = f));
                    Find.WindowStack.Add(new FloatMenu(facOpts));
                }
                listing.Gap(10f);

                listing.Label("3. 規模點數:");
                if (cqPointsBuf == null) cqPointsBuf = settings.customQuestPoints.ToString("F0");
                Rect ptsRect = listing.GetRect(30f);
                Widgets.TextFieldNumeric(ptsRect.LeftPart(0.2f), ref settings.customQuestPoints, ref cqPointsBuf, 0f, 100000f);
                float oldPts = settings.customQuestPoints;
                settings.customQuestPoints = Widgets.HorizontalSlider(ptsRect.RightPart(0.75f), settings.customQuestPoints, 0f, 20000f);
                if (settings.customQuestPoints != oldPts) cqPointsBuf = settings.customQuestPoints.ToString("F0");

                listing.Label("4. 生成人數 (輸入 -1 代表由系統預設決定):");
                if (cqPawnCountBuf == null) cqPawnCountBuf = settings.customQuestPawnCount.ToString();
                Rect countRect = listing.GetRect(30f);
                Widgets.TextFieldNumeric(countRect.LeftPart(0.2f), ref settings.customQuestPawnCount, ref cqPawnCountBuf, -1, 200);
                int oldCount = settings.customQuestPawnCount;
                settings.customQuestPawnCount = (int)Widgets.HorizontalSlider(countRect.RightPart(0.75f), settings.customQuestPawnCount, -1f, 200f);
                if (settings.customQuestPawnCount != oldCount) cqPawnCountBuf = settings.customQuestPawnCount.ToString();

                Rect cxenoRect = listing.GetRect(30f); Widgets.Label(cxenoRect.LeftPart(0.2f), "5. 人種選擇:");
                if (Widgets.ButtonText(cxenoRect.RightPart(0.75f), settings.customQuestXenotype?.LabelCap ?? "系統預設 (不介入生成)"))
                {
                    List<FloatMenuOption> xOptions = new List<FloatMenuOption> { new FloatMenuOption("系統預設 (不介入生成)", () => settings.customQuestXenotype = null) };
                    foreach (var x in DefDatabase<XenotypeDef>.AllDefs.OrderBy(d => d.label)) xOptions.Add(new FloatMenuOption(x.LabelCap, () => settings.customQuestXenotype = x, x.Icon, Color.white));
                    Find.WindowStack.Add(new FloatMenu(xOptions));
                }
                listing.Gap(10f);

                // 6. 年齡範圍修正 (Min & Max)
                listing.Label("6. 生成角色年齡範圍:");
                if (cqAgeMinBuf == null) cqAgeMinBuf = settings.customQuestAgeMin.ToString();
                if (cqAgeMaxBuf == null) cqAgeMaxBuf = settings.customQuestAgeMax.ToString();
                Rect ageRect = listing.GetRect(30f);

                Widgets.TextFieldNumeric(ageRect.LeftPart(0.1f), ref settings.customQuestAgeMin, ref cqAgeMinBuf, 14, 120);
                Widgets.TextFieldNumeric(new Rect(ageRect.x + ageRect.width * 0.15f, ageRect.y, ageRect.width * 0.1f, 30f), ref settings.customQuestAgeMax, ref cqAgeMaxBuf, 14, 120);

                IntRange ageRange = new IntRange(settings.customQuestAgeMin, settings.customQuestAgeMax);
                Widgets.IntRange(ageRect.RightPart(0.7f), 9991, ref ageRange, 14, 120);
                if (settings.customQuestAgeMin != ageRange.min || settings.customQuestAgeMax != ageRange.max)
                {
                    settings.customQuestAgeMin = ageRange.min; settings.customQuestAgeMax = ageRange.max;
                    cqAgeMinBuf = settings.customQuestAgeMin.ToString(); cqAgeMaxBuf = settings.customQuestAgeMax.ToString();
                }

                listing.Label("7. 性別比例:");
                Rect genRect = listing.GetRect(30f); string ratioBuf = settings.customQuestFemaleRatio.ToString("F0");
                Widgets.TextFieldNumeric(genRect.LeftPart(0.1f), ref settings.customQuestFemaleRatio, ref ratioBuf, 0f, 100f);
                Rect sliderArea = new Rect(genRect.x + genRect.width * 0.15f, genRect.y, genRect.width * 0.6f, 30f);
                Widgets.Label(new Rect(sliderArea.x, sliderArea.y, 50f, 30f), "只有男");
                settings.customQuestFemaleRatio = Widgets.HorizontalSlider(new Rect(sliderArea.x + 50f, sliderArea.y + 8f, sliderArea.width - 100f, 20f), settings.customQuestFemaleRatio, 0f, 100f);
                Widgets.Label(new Rect(sliderArea.xMax - 50f, sliderArea.y, 50f, 30f), "只有女");
                if (Widgets.ButtonText(genRect.RightPart(0.2f), "快速選擇 ▼"))
                {
                    List<FloatMenuOption> opts = new List<FloatMenuOption> { new FloatMenuOption("只有男 (0%)", () => settings.customQuestFemaleRatio = 0f), new FloatMenuOption("只有女 (100%)", () => settings.customQuestFemaleRatio = 100f) };
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
                listing.Gap(10f);

                listing.Label("8. 生成特性數量 (1-5):");
                Rect traitRect = listing.GetRect(30f); string minTraitBuf = settings.customQuestTraitsMin.ToString(); string maxTraitBuf = settings.customQuestTraitsMax.ToString();
                Widgets.TextFieldNumeric(traitRect.LeftPart(0.1f), ref settings.customQuestTraitsMin, ref minTraitBuf, 1, 5); Widgets.Label(new Rect(traitRect.x + traitRect.width * 0.12f, traitRect.y, 20f, 30f), "-");
                Widgets.TextFieldNumeric(new Rect(traitRect.x + traitRect.width * 0.15f, traitRect.y, traitRect.width * 0.1f, 30f), ref settings.customQuestTraitsMax, ref maxTraitBuf, 1, 5);
                IntRange tRange = new IntRange(settings.customQuestTraitsMin, settings.customQuestTraitsMax); Widgets.IntRange(traitRect.RightPart(0.7f), 9992, ref tRange, 1, 5);
                settings.customQuestTraitsMin = tRange.min; settings.customQuestTraitsMax = tRange.max;
                listing.Gap(15f);

                listing.CheckboxLabeled("9. 🎁 啟用自訂任務獎勵", ref settings.useCustomRewards, "開啟後將替換系統獎品。若任務不支援會彈出警告，請關閉後再生成。");
                if (settings.useCustomRewards)
                {
                    listing.Label($"   (目前設定: {settings.customRewards.Count}/6)");
                    if (Widgets.ButtonText(listing.GetRect(35f).LeftPart(0.4f), "點此設定自訂獎勵清單...")) Find.WindowStack.Add(new Dialog_CustomRewards());
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

            Rect btnRect = listing.GetRect(50f); Text.Font = GameFont.Medium;
            if (Widgets.ButtonText(btnRect, "🚀 立即生成任務"))
            {
                if (isComplex && settings.customQuestRequireSite)
                {
                    Find.WindowStack.TryRemove(typeof(Dialog_ModSettings)); Find.WorldSelector.ClearSelection(); Find.World.renderer.wantedMode = WorldRenderMode.Planet;
                    Find.WorldTargeter.BeginTargeting(new System.Func<GlobalTargetInfo, bool>(target =>
                    {
                        if (target.IsValid && target.Tile >= 0) QuestGenerator.GenerateQuestFinal(target.Tile); return true;
                    }), true);
                }
                else QuestGenerator.GenerateQuestFinal(-1);
            }
            Text.Font = GameFont.Small;
            listing.End(); Widgets.EndScrollView();
        }
    }

    public class MainButtonWorker_QuestScheduler : MainButtonWorker { public override void Activate() => Find.WindowStack.Add(new Dialog_ModSettings(QuestSchedulerMod.instance)); }
}