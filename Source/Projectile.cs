﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;
using UnityEngine;
using System.Reflection;

namespace RimThreaded
{

	public abstract class Projectile_Patch 
	{

		public static AccessTools.FieldRef<Projectile, Vector3> origin =
			AccessTools.FieldRefAccess<Projectile, Vector3>("origin");
		public static AccessTools.FieldRef<Projectile, ThingDef> equipmentDef =
			AccessTools.FieldRefAccess<Projectile, ThingDef>("equipmentDef");
		public static AccessTools.FieldRef<Projectile, Vector3> destination =
			AccessTools.FieldRefAccess<Projectile, Vector3>("destination");
		public static AccessTools.FieldRef<Projectile, bool> landed =
			AccessTools.FieldRefAccess<Projectile, bool>("landed");
		public static AccessTools.FieldRef<Projectile, int> ticksToImpact =
			AccessTools.FieldRefAccess<Projectile, int>("ticksToImpact");
		public static AccessTools.FieldRef<Projectile, Thing> launcher =
			AccessTools.FieldRefAccess<Projectile, Thing>("launcher");
		public static AccessTools.FieldRef<Projectile, Sustainer> ambientSustainer =
			AccessTools.FieldRefAccess<Projectile, Sustainer>("ambientSustainer");
		public static AccessTools.FieldRef<Projectile, ThingDef> targetCoverDef =
			AccessTools.FieldRefAccess<Projectile, ThingDef>("targetCoverDef");


        //public static MethodInfo ThrowDebugText =
        //typeof(Projectile).GetMethod("ThrowDebugText", BindingFlags.NonPublic | BindingFlags.Instance);
        //public static MethodInfo CanHit =
        //typeof(Projectile).GetMethod("CanHit", BindingFlags.NonPublic | BindingFlags.Instance);
        //public static MethodInfo ImpactReflected =
        //typeof(Projectile).GetMethod("Impact", BindingFlags.NonPublic | BindingFlags.Instance);

        //public static MethodInfo ImpactReflected = AccessTools.Method();

        public static void Impact(Projectile __instance, Thing hitThing)
        {
			if(__instance is Bullet bullet)
            {
				Bullet_Patch.Impact(bullet, hitThing);
            }
			else if (__instance is Projectile_DoomsdayRocket projectile_DoomsdayRocket)
			{
				Projectile_DoomsdayRocket_Patch.Impact(projectile_DoomsdayRocket, hitThing);
			}
			else if (__instance is Spark spark)
			{
				Spark_Patch.Impact(spark, hitThing);
			}
			else if (__instance is WaterSplash waterSplash)
			{
				WaterSplash_Patch.Impact(waterSplash, hitThing);
			}
			else if (__instance is Projectile_Explosive projectile_Explosive)
			{
				Projectile_Explosive_Patch.Impact(projectile_Explosive, hitThing);
			}
			else
            {
				Base_Impact(__instance, hitThing);
			}

		}

        public static void Base_Impact(Projectile projectile, Thing hitThing)
        {
			GenClamor.DoClamor(projectile, 2.1f, ClamorDefOf.Impact);
			projectile.Destroy();
		}

        public static void OverrideImpact(Projectile projectile, Thing hitThing)
        {
            //简单来说这个类就是调用自己的Impact函数不用每个类都转成 Projectile 然后在判断调用
            AccessTools.Method(projectile.GetType(), "Impact").Invoke(projectile, new object[] { hitThing });
        }

        private static void ThrowDebugText(Projectile __instance, string text, IntVec3 c)
		{
			if (DebugViewSettings.drawShooting)
			{
				MoteMaker.ThrowText(c.ToVector3Shifted(), __instance.Map, text, -1f);
			}
		}
		
		
		private static bool CanHit(Projectile __instance, Thing thing)
		{
			if (!thing.Spawned)
			{
				return false;
			}
			if (thing == __instance.Launcher)
			{
				return false;
			}
			bool flag = false;
			foreach (IntVec3 c in thing.OccupiedRect())
			{
                List<Thing> thingList = c.GetThingList(__instance.Map); 
				bool flag2 = false;
                for (int i = 0; i < thingList.Count; i++)
                {
                    if (thingList[i] != thing && thingList[i].def.Fillage == FillCategory.Full && thingList[i].def.Altitude >= thing.def.Altitude)
                    {
                        flag2 = true;
                        break;
                    }
                }
                if (!flag2)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return false;
			}
			ProjectileHitFlags hitFlags = __instance.HitFlags;
			if (thing == __instance.intendedTarget && (hitFlags & ProjectileHitFlags.IntendedTarget) != ProjectileHitFlags.None)
			{
				return true;
			}
			if (thing != __instance.intendedTarget)
			{
				if (thing is Pawn)
				{
					if ((hitFlags & ProjectileHitFlags.NonTargetPawns) != ProjectileHitFlags.None)
					{
						return true;
					}
				}
				else if ((hitFlags & ProjectileHitFlags.NonTargetWorld) != ProjectileHitFlags.None)
				{
					return true;
				}
			}
			return thing == __instance.intendedTarget && thing.def.Fillage == FillCategory.Full;
		}

		public static bool ImpactSomething(Projectile __instance)
		{
			if (__instance.def.projectile.flyOverhead)
			{
				RoofDef roofDef = __instance.Map.roofGrid.RoofAt(__instance.Position);
				if (roofDef != null)
				{
					if (roofDef.isThickRoof)
					{
						ThrowDebugText(__instance, "hit-thick-roof", __instance.Position);
						__instance.def.projectile.soundHitThickRoof.PlayOneShot((SoundInfo)new TargetInfo(__instance.Position, __instance.Map, false));
						__instance.Destroy(DestroyMode.Vanish);
						return false;
					}
					if (__instance.Position.GetEdifice(__instance.Map) == null || __instance.Position.GetEdifice(__instance.Map).def.Fillage != FillCategory.Full)
						RoofCollapserImmediate.DropRoofInCells(__instance.Position, __instance.Map, (List<Thing>)null);
				}
			}
			if (__instance.usedTarget.HasThing && CanHit(__instance, __instance.usedTarget.Thing))
			{
				if (__instance.usedTarget.Thing is Pawn thing && thing.GetPosture() != PawnPosture.Standing && ((double)(origin(__instance) - destination(__instance)).MagnitudeHorizontalSquared() >= 20.25 && !Rand.Chance(0.2f)))
				{
					ThrowDebugText(__instance, "miss-laying", __instance.Position);
                    //Impact.Invoke(__instance, new object[] { (Thing)null });
                    OverrideImpact(__instance, null);
				}
				else
				{
                    OverrideImpact(__instance, __instance.usedTarget.Thing);
				}
			}
			else
			{
				List<Thing> cellThingsFiltered = new List<Thing>();
				List<Thing> thingList = __instance.Position.GetThingList(__instance.Map);
				for (int index = 0; index < thingList.Count; ++index)
				{
					Thing thing = thingList[index];
					if ((thing.def.category == ThingCategory.Building || thing.def.category == ThingCategory.Pawn || (thing.def.category == ThingCategory.Item || thing.def.category == ThingCategory.Plant)) && CanHit(__instance, thing))
						cellThingsFiltered.Add(thing);
				}
				cellThingsFiltered.Shuffle<Thing>();
				for (int index = 0; index < cellThingsFiltered.Count; ++index)
				{
					Thing thing = cellThingsFiltered[index];
					float num;
					if (thing is Pawn p)
					{
						num = 0.5f * Mathf.Clamp(p.BodySize, 0.1f, 2f);
						if (p.GetPosture() != PawnPosture.Standing && (double)(origin(__instance) - destination(__instance)).MagnitudeHorizontalSquared() >= 20.25)
							num *= 0.2f;
						if (launcher(__instance) != null && p.Faction != null && (launcher(__instance).Faction != null && !p.Faction.HostileTo(launcher(__instance).Faction)))
							num *= VerbUtility.InterceptChanceFactorFromDistance(origin(__instance), __instance.Position);
					}
					else
						num = 1.5f * thing.def.fillPercent;
					if (Rand.Chance(num))
					{
						ThrowDebugText(__instance, "hit-" + num.ToStringPercent(), __instance.Position);
                        OverrideImpact(__instance, cellThingsFiltered.RandomElement<Thing>());
						return false;
					}
					ThrowDebugText(__instance, "miss-" + num.ToStringPercent(), __instance.Position);
				}
                OverrideImpact(__instance, null);
			}
			return false;
		}
		
		
		private static bool CheckForFreeIntercept(Projectile __instance, IntVec3 c)
		{
			if (destination(__instance).ToIntVec3() == c)
				return false;
			float num1 = VerbUtility.InterceptChanceFactorFromDistance(origin(__instance), c);
			if ((double)num1 <= 0.0)
				return false;
			bool flag1 = false;
			List<Thing> thingList = c.GetThingList(__instance.Map);
			for (int index = 0; index < thingList.Count; ++index)
			{
				Thing thing;
				try
				{
					thing = thingList[index];
				} catch(ArgumentOutOfRangeException)
                {
					break;
                }
				if (thing != null)
				{
					if (CanHit(__instance, thing))
					{
						bool flag2 = false;
						if (thing.def.Fillage == FillCategory.Full)
						{
							if (thing is Building_Door buildingDoor && buildingDoor.Open)
							{
								flag2 = true;
							}
							else
							{
								ThrowDebugText(__instance, "int-wall", c);
								Impact(__instance, thing);
								return true;
							}
						}
						float num2 = 0.0f;
						if (thing is Pawn p)
						{
							num2 = 0.4f * Mathf.Clamp(p.BodySize, 0.1f, 2f);
							if (p.GetPosture() != PawnPosture.Standing)
								num2 *= 0.1f;
							if (launcher(__instance) != null && p.Faction != null && (launcher(__instance).Faction != null && !p.Faction.HostileTo(launcher(__instance).Faction)))
								num2 *= Find.Storyteller.difficultyValues.friendlyFireChanceFactor;
						}
						else if ((double)thing.def.fillPercent > 0.200000002980232)
							num2 = !flag2 ? (!new IntVec3(destination(__instance)).AdjacentTo8Way(c) ? thing.def.fillPercent * 0.15f : thing.def.fillPercent * 1f) : 0.05f;
						float num3 = num2 * num1;
						if ((double)num3 > 9.99999974737875E-06)
						{
							if (Rand.Chance(num3))
							{
								ThrowDebugText(__instance, "int-" + num3.ToStringPercent(), c);
								Impact(__instance, thing);
								return true;
							}
							flag1 = true;
							ThrowDebugText(__instance, num3.ToStringPercent(), c);
						}
					}
				}
			}
			if (!flag1)
				ThrowDebugText(__instance, "o", c);
			return false;
		}
		
		
		private static bool CheckForFreeInterceptBetween(Projectile __instance, Vector3 lastExactPos, Vector3 newExactPos)
		{
			if (lastExactPos == newExactPos)
				return false;
			List<Thing> thingList = __instance.Map.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor);
			for (int index = 0; index < thingList.Count; ++index)
			{
				if (thingList[index].TryGetComp<CompProjectileInterceptor>().CheckIntercept(__instance, lastExactPos, newExactPos))
				{
					__instance.Destroy(DestroyMode.Vanish);
					return true;
				}
			}
			IntVec3 intVec3_1 = lastExactPos.ToIntVec3();
			IntVec3 intVec3_2 = newExactPos.ToIntVec3();
			if (intVec3_2 == intVec3_1 || !intVec3_1.InBounds(__instance.Map) || !intVec3_2.InBounds(__instance.Map))
				return false;
			if (intVec3_2.AdjacentToCardinal(intVec3_1))
				return CheckForFreeIntercept(__instance, intVec3_2);
			if ((double)VerbUtility.InterceptChanceFactorFromDistance(origin(__instance), intVec3_2) <= 0.0)
				return false;
			Vector3 vect = lastExactPos;
			Vector3 v = newExactPos - lastExactPos;
			Vector3 vector3 = v.normalized * 0.2f;
			int num1 = (int)((double)v.MagnitudeHorizontal() / 0.200000002980232);
			//Projectile.checkedCells.Clear();
			List<IntVec3> checkedCells = new List<IntVec3>();
			int num2 = 0;
			IntVec3 intVec3_3;
			do
			{
				vect += vector3;
				intVec3_3 = vect.ToIntVec3();
				if (!checkedCells.Contains(intVec3_3))
				{
					if (CheckForFreeIntercept(__instance,intVec3_3))
						return true;
					checkedCells.Add(intVec3_3);
				}
				++num2;
			}
			while (num2 <= num1 && !(intVec3_3 == intVec3_2));
			return false;
		}

		public static AccessTools.FieldRef<ThingWithComps, List<ThingComp>> comps =
			AccessTools.FieldRefAccess<ThingWithComps, List<ThingComp>>("comps");
		
		
		public static bool Tick(Projectile __instance)
		{
			if(__instance is ThingWithComps twc)
            {
                if (comps(twc) != null)
				{
					int index = 0;
					for (int count = comps(twc).Count; index < count; ++index)
						comps(twc)[index].CompTick();
				}
			}
			
			if (landed(__instance))
				return false;
			Vector3 exactPosition1 = __instance.ExactPosition;
			--ticksToImpact(__instance);
			if (!__instance.ExactPosition.InBounds(__instance.Map))
			{
				++ticksToImpact(__instance);
				__instance.Position = __instance.ExactPosition.ToIntVec3();
				if (!__instance.Destroyed)
				{
					__instance.Destroy(DestroyMode.Vanish);
				}
			}
			else
			{
				Vector3 exactPosition2 = __instance.ExactPosition;
				if (CheckForFreeInterceptBetween(__instance, exactPosition1, exactPosition2))
					return false;
				__instance.Position = __instance.ExactPosition.ToIntVec3();
				if (ticksToImpact(__instance) == 60 && Find.TickManager.CurTimeSpeed == TimeSpeed.Normal && __instance.def.projectile.soundImpactAnticipate != null)
					__instance.def.projectile.soundImpactAnticipate.PlayOneShot((SoundInfo)(Thing)__instance);
				if (ticksToImpact(__instance) <= 0)
				{
					if (new IntVec3(destination(__instance)).InBounds(__instance.Map))
						__instance.Position = new IntVec3(destination(__instance));
					ImpactSomething(__instance);
				}
				else
				{
					if (ambientSustainer(__instance) == null)
						return false;
					ambientSustainer(__instance).Maintain();
				}
			}
			return false;
		}
		
	}
}
