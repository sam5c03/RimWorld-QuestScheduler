using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace QuestScheduler
{
    public static class QuestGenerator
    {
        public static bool IsComplexQuest(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return false;
            string low = defName.ToLower();

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

        public static void GenerateQuestFinal(int tileIndex)
        {
            var settings = QuestSchedulerMod.settings;
            QuestScriptDef qDef = DefDatabase<QuestScriptDef>.GetNamedSilentFail(settings.customQuestDefName);
            if (qDef == null) return;

            bool isComplex = IsComplexQuest(settings.customQuestDefName);
            Slate slate = new Slate();
            slate.Set("discoveryMethod", "發現了");

            if (isComplex)
            {
                CustomRaidGenerator.minAge = settings.customQuestAgeMin;
                CustomRaidGenerator.maxAge = settings.customQuestAgeMax;
                CustomRaidGenerator.targetFemaleRatio = settings.customQuestFemaleRatio;
                settings.forcedTraitCountMin = settings.customQuestTraitsMin;
                settings.forcedTraitCountMax = settings.customQuestTraitsMax;
                CustomRaidGenerator.forcedXeno = settings.customQuestXenotype;
                CustomRaidGenerator.isSpawningCustomQuest = true;

                slate.Set("points", settings.customQuestPoints);
                if (QuestSchedulerMod.customQuestFaction != null) slate.Set("faction", QuestSchedulerMod.customQuestFaction);

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
                    if (tileIndex >= 0)
                    {
                        foreach (var part in quest.PartsListForReading.OfType<QuestPart_SpawnWorldObject>())
                            if (part.worldObject != null) part.worldObject.Tile = tileIndex;
                    }

                    foreach (var part in quest.PartsListForReading.OfType<QuestPart_SpawnWorldObject>())
                    {
                        if (part.worldObject != null && part.worldObject.Tile >= 0)
                        {
                            if (!settings.customSiteTiles.Contains(part.worldObject.Tile))
                                settings.customSiteTiles.Add(part.worldObject.Tile);
                            Log.Message($"[QuestScheduler] 據點已記憶！真實地塊座標: {part.worldObject.Tile}");
                        }
                    }

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
                                if (def != null) { Thing t = ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def)); t.stackCount = r.amount; itemsToDrop.Add(t); }
                            }
                            else if (r.type == RewardType.Goodwill)
                            {
                                Faction fac = Find.FactionManager.AllFactionsVisible.FirstOrDefault(f => f.def.defName == r.factionDefName);
                                if (fac != null) { newChoice.rewards.Add(new Reward_Goodwill() { faction = fac, amount = r.amount }); quest.AddPart(new QuestPart_FactionGoodwillChange() { faction = fac, change = r.amount, inSignal = successSignal }); }
                            }
                            else if (r.type == RewardType.Honor)
                            {
                                Faction fac = Find.FactionManager.AllFactionsVisible.FirstOrDefault(f => f.def.defName == r.factionDefName);
                                if (fac != null) { newChoice.rewards.Add(new Reward_RoyalFavor() { faction = fac, amount = r.amount }); quest.AddPart(new QuestPart_GiveRoyalFavor() { faction = fac, amount = r.amount, inSignal = successSignal, giveToAccepter = true }); }
                            }
                        }

                        if (itemsToDrop.Count > 0)
                        {
                            Reward_Items rwItem = new Reward_Items(); rwItem.items.AddRange(itemsToDrop); newChoice.rewards.Add(rwItem);
                            quest.AddPart(new QuestPart_DropPods() { Things = itemsToDrop, inSignal = successSignal, mapParent = Find.AnyPlayerHomeMap?.Parent });
                        }

                        choicePart.choices.Add(newChoice);
                        var partsToRemove = quest.PartsListForReading.Where(p => (p is QuestPart_DropPods && p != quest.PartsListForReading.Last()) || p is QuestPart_GiveRoyalFavor || p is QuestPart_FactionGoodwillChange).ToList();
                        foreach (var p in partsToRemove) quest.RemovePart(p);
                    }

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
                    Messages.Message("任務生成失敗，可能是因為地塊限制 (例如離家太近) 或派系不符合條件。", MessageTypeDefOf.RejectInput);
                }
            }
            finally
            {
                CustomRaidGenerator.isSpawningCustomQuest = false;
                CustomRaidGenerator.forcedXeno = null;
            }
        }
    }
}