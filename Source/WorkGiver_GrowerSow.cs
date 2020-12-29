using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace RimThreaded
{
    //种地w
    class WorkGiver_GrowerSow_Patch
    {
        public static bool JobOnCell(WorkGiver_GrowerSow __instance, ref Job __result, Pawn pawn, IntVec3 c, bool forced = false)
        {
            Map map = pawn.Map;
            if (c.IsForbidden(pawn))
            {
                goto END;
            }
            if (!PlantUtility.GrowthSeasonNow(c, map, true))
            {
                goto END;
            }

        END:
            return false;
        }
    }
}
