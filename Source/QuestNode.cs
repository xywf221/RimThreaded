using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Verse;
using HarmonyLib;
using System.Reflection;

namespace RimThreaded
{
    class QuestNode_Patch
    {
        public static MethodInfo TestRunInt = AccessTools.Method(typeof(QuestNode), "TestRunInt");

        public static bool TestRun(QuestNode __instance, ref bool __result, Slate slate)
        {
            int tid = Thread.CurrentThread.ManagedThreadId;
            lock (RimThreaded.timeoutExemptThreads2)
            {
                RimThreaded.timeoutExemptThreads2.Add(tid, 30000); //30 s
            }

            //尝试下看看行不行
            try
            {
                Action<QuestNode, Slate> action;
                if (slate.TryGet<Action<QuestNode, Slate>>("testRunCallback", out action, false) && action != null)
                {
                    action(__instance, slate);
                }
                __result = (bool)TestRunInt.Invoke(__instance, new object[] {slate });
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat(new object[]
                {
                    "Exception test running ",
                    __instance.GetType().Name,
                    ": ",
                    ex,
                    "\n\nSlate vars:\n",
                    slate.ToString()
                }), false);
                __result = false;
            }

            return false;
        }
    }
}
