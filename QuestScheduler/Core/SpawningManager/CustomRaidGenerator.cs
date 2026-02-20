using RimWorld;
using System.Linq;
using Verse;

namespace QuestScheduler
{
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
}