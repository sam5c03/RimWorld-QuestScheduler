using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace QuestScheduler
{
    public class QuestSchedulerMod : Mod
    {
        public static QuestSchedulerSettings settings;
        public static QuestSchedulerMod instance;
        public static Faction customQuestFaction = null;

        public QuestSchedulerMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<QuestSchedulerSettings>();
            instance = this;
            new Harmony("com.questschedulermod.main").PatchAll();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // 將繁雜的繪製邏輯外包給 SettingsUI 類別
            SettingsUI.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory() => "Quest Scheduler Pro";
    }

    [HarmonyPatch(typeof(Game), "LoadGame")]
    public static class Patch_ResetStaleReferences
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            QuestSchedulerMod.customQuestFaction = null;
            Log.Message("[QuestScheduler] 偵測到存檔載入，已自動重置派系引用以防止關係損壞。");
        }
    }

    [HarmonyPatch(typeof(GenScene), "GoToMainMenu")]
    public static class Patch_ResetOnExit
    {
        [HarmonyPostfix]
        public static void Postfix() { QuestSchedulerMod.customQuestFaction = null; }
    }
}