﻿using RimWorld.Planet;
using System;
using System.Threading;
using Verse;
using Verse.Sound;

namespace RimThreaded
{
	class SoundStarter_Patch
	{
		public static bool PlayOneShot(SoundDef soundDef, SoundInfo info)
		{
			if (UnityData.IsInMainThread)
			{
				return true;
			}

			if (soundDef == null)
			{
				Log.Error("Tried to PlayOneShot with null SoundDef. Info=" + info, false);
				return false;
			}
			DebugSoundEventsLog.Notify_SoundEvent(soundDef, info);
			if (soundDef == null)
			{
				return false;
			}
			if (soundDef.isUndefined)
			{
				return false;
			}
			if (soundDef.sustain)
			{
				Log.Error("Tried to play sustainer SoundDef " + soundDef + " as a one-shot sound.", false);
				return false;
			}
			for (int i = 0; i < soundDef.subSounds.Count; i++)
			{
				RimThreaded.PlayOneShot.Enqueue(new Tuple<SoundDef, SoundInfo>(soundDef, info));
			}
			// Don't know why but if this is set to false, threads will hang and timeout.
			return true;
		}

		public static bool PlayOneShotOnCamera(SoundDef soundDef, Map onlyThisMap)
		{
			if (UnityData.IsInMainThread)
			{
				return true;
			}

			if (onlyThisMap != null && (Find.CurrentMap != onlyThisMap || WorldRendererUtility.WorldRenderedNow))
			{
				return false;
			}
			if (soundDef == null)
			{
				return false;
			}

			RimThreaded.PlayOneShotCamera.Enqueue(new Tuple<SoundDef, Map>(soundDef, onlyThisMap));
			return false;
		}
		public static bool TrySpawnSustainer(ref Sustainer __result, SoundDef soundDef, SoundInfo info)
		{
			DebugSoundEventsLog.Notify_SoundEvent(soundDef, info);
			if (soundDef == null)
			{
				__result = null;
				return false;
			}
			if (soundDef.isUndefined)
			{
				__result = null;
				return false;
			}
			if (!soundDef.sustain)
			{
				Log.Error("Tried to spawn a sustainer from non-sustainer sound " + soundDef + ".", false);
				__result = null;
				return false;
			}
			if (!info.IsOnCamera && info.Maker.Thing != null && info.Maker.Thing.Destroyed)
			{
				__result = null;
				return false;
			}
			if (soundDef.sustainStartSound != null)
			{
				soundDef.sustainStartSound.PlayOneShot(info);
			}

            int tID = Thread.CurrentThread.ManagedThreadId;
            if (RimThreaded.mainRequestWaits.TryGetValue(tID, out EventWaitHandle eventWaitStart))
			{
				lock (RimThreaded.newSustainerRequests)
				{
					RimThreaded.newSustainerRequests[tID] = new object[] { soundDef, info };
				}
				RimThreaded.mainThreadWaitHandle.Set();
				eventWaitStart.WaitOne();

                if (!RimThreaded.newSustainerResults.TryGetValue(tID, out Sustainer sustainer))
                {
                    Log.Error("Error retriving newSustainerResults");
                }
                __result = sustainer;
			} else
            {
				__result = new Sustainer(soundDef, info);
			}
			//return new Sustainer(soundDef, info);
			//return sustainer;
			return false;

		}
	}
}