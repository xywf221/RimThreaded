﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;
using UnityEngine;
using Verse.AI;
using RimWorld;
using Verse.Sound;
using RimWorld.Planet;
using System.Collections.Concurrent;
using System.Threading;
using System.Diagnostics;
using System.Reflection.Emit;
using UnityEngine.Experimental.Rendering;
using static Verse.ImmunityHandler;

namespace RimThreaded
{
    [StaticConstructorOnStartup]
    public class RimThreaded
    {
        public static DateTime lastClosestThingGlobal = DateTime.Now;

        public static int maxThreads = Math.Max(int.Parse(RimThreadedMod.Settings.maxThreadsBuffer), 1);
        public static int timeoutMS = Math.Max(int.Parse(RimThreadedMod.Settings.timeoutMSBuffer), 1);
        public static int timeoutMS2 = 300000; //five minute timeout for timeout exempt threads
        public static bool suppressTexture2dError = RimThreadedMod.Settings.suppressTexture2dError;
        public static float timeSpeedNormal = float.Parse(RimThreadedMod.Settings.timeSpeedNormalBuffer);
        public static float timeSpeedFast = float.Parse(RimThreadedMod.Settings.timeSpeedFastBuffer);
        public static float timeSpeedSuperfast = float.Parse(RimThreadedMod.Settings.timeSpeedSuperfastBuffer);
        public static float timeSpeedUltrafast = float.Parse(RimThreadedMod.Settings.timeSpeedUltrafastBuffer);
        public static DateTime lastTicksCheck = DateTime.Now;
        public static int lastTicksAbs = -1;
        public static int ticksPerSecond = 0;

        public static EventWaitHandle mainThreadWaitHandle = new AutoResetEvent(false);
        public static EventWaitHandle monitorThreadWaitHandle = new AutoResetEvent(false);
        public static Dictionary<int, EventWaitHandle> eventWaitStarts = new Dictionary<int, EventWaitHandle>();

        public static Dictionary<int, EventWaitHandle> eventWaitDones = new Dictionary<int, EventWaitHandle>();

        //public static ConcurrentQueue<Thing> drawQueue = new ConcurrentQueue<Thing>();
        private static Dictionary<int, Thread> allThreads = new Dictionary<int, Thread>();
        private static Thread monitorThread = null;
        private static bool allWorkerThreadsFinished = false;
        public static bool SingleTickComplete = true;

        //MainThreadRequests
        //主线程 部分函数只能再主函数中调用
        public static Dictionary<int, EventWaitHandle> mainRequestWaits = new Dictionary<int, EventWaitHandle>();
        public static Dictionary<int, object[]> tryMakeAndPlayRequests = new Dictionary<int, object[]>();

        //用于函数调用。也是再主线程里面跑的 request写入请求 results读取结果
        public static Dictionary<int, object[]> safeFunctionRequests =
            new Dictionary<int, object[]>();
        public static Dictionary<int, object> safeFunctionResults =
            new Dictionary<int, object>();


        public static Queue<int> buildDatabase = new Queue<int>();
        public static Dictionary<int, SampleSustainer> tryMakeAndPlayResults = new Dictionary<int, SampleSustainer>();
        public static Dictionary<int, object[]> newSustainerRequests = new Dictionary<int, object[]>();
        public static Dictionary<int, Sustainer> newSustainerResults = new Dictionary<int, Sustainer>();
        public static Dictionary<int, object[]> planeMeshRequests = new Dictionary<int, object[]>();
        public static Dictionary<int, Mesh> planeMeshResults = new Dictionary<int, Mesh>();
        public static Dictionary<string, Texture2D> texture2DResults = new Dictionary<string, Texture2D>();
        public static Dictionary<int, string> texture2DRequests = new Dictionary<int, string>();
        public static Dictionary<int, Mesh> meshRequests = new Dictionary<int, Mesh>();
        public static Dictionary<int, Mesh> meshResults = new Dictionary<int, Mesh>();
        public static Dictionary<MaterialRequest, Material> materialResults = new Dictionary<MaterialRequest, Material>();
        public static Dictionary<int, MaterialRequest> materialRequests = new Dictionary<int, MaterialRequest>();
        public static Dictionary<int, LayerSubMesh> layerSubMeshResults = new Dictionary<int, LayerSubMesh>();
        public static Dictionary<int, object[]> layerSubMeshRequests = new Dictionary<int, object[]>();
        public static Dictionary<int, Map> generateMapResults = new Dictionary<int, Map>();
        public static Dictionary<int, object[]> generateMapRequests = new Dictionary<int, object[]>();
        public static Dictionary<int, AudioSource> newAudioSourceResults = new Dictionary<int, AudioSource>();
        public static Dictionary<int, GameObject> newAudioSourceRequests = new Dictionary<int, GameObject>();
        public static Dictionary<int, RenderTexture> renderTextureResults = new Dictionary<int, RenderTexture>();
        public static Dictionary<int, object[]> renderTextureRequests = new Dictionary<int, object[]>();
        public static Dictionary<int, RenderTexture> renderTextureAAResults = new Dictionary<int, RenderTexture>();
        public static Dictionary<int, object[]> renderTextureAARequests = new Dictionary<int, object[]>();
        public static Dictionary<int, RenderTexture> renderTextureSetActiveRequests = new Dictionary<int, RenderTexture>();
        public static Dictionary<int, RenderTexture> renderTextureGetActiveRequests = new Dictionary<int, RenderTexture>();
        public static Dictionary<int, RenderTexture> renderTextureGetActiveResults = new Dictionary<int, RenderTexture>();
        public static Dictionary<int, object[]> texture2dRequests = new Dictionary<int, object[]>();
        public static Dictionary<int, Texture2D> texture2dResults = new Dictionary<int, Texture2D>();
        public static Dictionary<int, object[]> calcHeightRequests = new Dictionary<int, object[]>();
        public static Dictionary<int, float> calcHeightResults = new Dictionary<int, float>();
        public static Dictionary<int, Texture2D> getReadableTextureRequests = new Dictionary<int, Texture2D>();
        public static Dictionary<int, Texture2D> getReadableTextureResults = new Dictionary<int, Texture2D>();
        public static Dictionary<int, object[]> blitRequests = new Dictionary<int, object[]>();
        public static Dictionary<int, object[]> internal_CreateRequests = new Dictionary<int, object[]>();
        public static Dictionary<int, object[]> readPixelRequests = new Dictionary<int, object[]>();
        public static Dictionary<int, object[]> applyTextureRequests = new Dictionary<int, object[]>();
        public static Dictionary<int, RenderTexture> releaseTemporaryRequests = new Dictionary<int, RenderTexture>();
        public static Dictionary<int, RenderTexture> setActiveTextureRequests = new Dictionary<int, RenderTexture>();

        public static Dictionary<int, Mesh> newBoltMeshResults = new Dictionary<int, Mesh>();
        public static ConcurrentQueue<int> newBoltMeshRequests = new ConcurrentQueue<int>();

        //public static HashSet<int> timeoutExemptThreads = new HashSet<int>();
        public static Dictionary<int, int> timeoutExemptThreads2 = new Dictionary<int, int>();

        public static ConcurrentQueue<Tuple<SoundDef, SoundInfo>> PlayOneShot = new ConcurrentQueue<Tuple<SoundDef, SoundInfo>>();
        public static ConcurrentQueue<Tuple<SoundDef, Map>> PlayOneShotCamera = new ConcurrentQueue<Tuple<SoundDef, Map>>();

        public static Stopwatch stopwatch = new Stopwatch();

        //ThingListTicks
        public static List<Thing> thingListNormal;
        public static int thingListNormalTicks = 0;
        public static List<Thing> thingListRare;
        public static int thingListRareTicks = 0;
        public static List<Thing> thingListLong;
        public static int thingListLongTicks = 0;

        //SteadyEnvironmentEffects
        public static MapCellsInRandomOrder steadyEnvironmentEffectsCellsInRandomOrder = null;
        public static int steadyEnvironmentEffectsTicks = 0;
        public static int steadyEnvironmentEffectsArea = 0;
        public static int steadyEnvironmentEffectsCycleIndex = 0;
        public static SteadyEnvironmentEffects steadyEnvironmentEffectsInstance = null;
        public static int steadyEnvironmentEffectsCycleIndexOffset = 0;

        //WorldObjectsHolder
        public static WorldObjectsHolder worldObjectsHolder = null;
        public static int worldObjectsTicks = 0;
        public static List<WorldObject> worldObjects = null;

        //WorldPawns
        public static WorldPawns worldPawns = null;
        public static int worldPawnsTicks = 0;
        public static List<Pawn> worldPawnsAlive = null;

        //WindManager
        public static int plantMaterialsCount = 0;
        public static float plantSwayHead = 0;

        //FactionManager
        public static List<Faction> allFactions = null;
        public static int allFactionsTicks = 0;

        //WildPlantSpawner
        public static int WildPlantSpawnerTicks = 0;
        public static int WildPlantSpawnerCycleIndexOffset = 0;
        public static int WildPlantSpawnerArea = 0;
        public static Map WildPlantSpawnerMap = null;
        public static MapCellsInRandomOrder WildPlantSpawnerCellsInRandomOrder = null;
        public static float WildPlantSpawnerCurrentPlantDensity = 0f;
        public static float DesiredPlants = 0f;
        public static float DesiredPlantsTmp = 0f;
        public static int DesiredPlants1000 = 0;
        public static int DesiredPlantsTmp1000 = 0;
        public static int DesiredPlants2Tmp1000 = 0;
        public static int FertilityCellsTmp = 0;
        public static int FertilityCells2Tmp = 0;
        public static int FertilityCells = 0;
        public static WildPlantSpawner WildPlantSpawnerInstance = null;
        public static float WildPlantSpawnerChance = 0f;

        //TradeShip
        public static int TradeShipTicks = 0;
        public static ThingOwner TradeShipThings = null;

        //WorldComponents
        public static int WorldComponentTicks = 0;
        public static List<WorldComponent> WorldComponents = null;

        public static int currentPrepsDone = -1;
        public static readonly int totalPrepsCount = 11;
        public static List<EventWaitHandle> prepEventWaitStarts = new List<EventWaitHandle>();
        public static EventWaitHandle ProcessTicksManualWait = new ManualResetEvent(false);
        public static EventWaitHandle WaitingForAllThreadsToComplete = new ManualResetEvent(false);
        public static int workingOnMapPreTick = -1;
        public static int workingOnTickListNormal = -1;
        public static int workingOnTickListRare = -1;
        public static int workingOnTickListLong = -1;
        public static int workingOnDateNotifierTick = -1;
        public static int workingOnWorldTick = -1;
        public static int workingOnMapPostTick = -1;
        public static int workingOnHistoryTick = -1;
        public static int workingOnMiscellaneous = -1;
        public static TickManager currentInstance;
        public static int listsFullyProcessed = 0;

        static RimThreaded()
        {
            int tID = Thread.CurrentThread.ManagedThreadId;
            CreateWorkerThreads();
            ImmunityHandler_Patch.immunityInfoLists[tID] = new List<ImmunityInfo>();

            monitorThread = new Thread(() => MonitorThreads());
            monitorThread.Start();
            for (int index = 0; index < totalPrepsCount; index++)
            {
                prepEventWaitStarts.Add(new ManualResetEvent(false));
            }
        }

        public static void RestartAllWorkerThreads()
        {
            foreach (int tID2 in eventWaitDones.Keys.ToArray())
            {
                AbortWorkerThread(tID2);
            }
            CreateWorkerThreads();
        }

        private static void CreateWorkerThreads()
        {
            while (allThreads.Count < maxThreads)
            {
                CreateWorkerThread();
            }
        }

        private static void CreateWorkerThread()
        {
            Thread thread = new Thread(() => ProcessTicks());
            int tID = thread.ManagedThreadId;
            allThreads.Add(tID, thread);
            lock (eventWaitStarts)
            {
                eventWaitStarts[tID] = new AutoResetEvent(false);
            }
            lock (eventWaitDones)
            {
                eventWaitDones[tID] = new AutoResetEvent(false);
            }
            lock (mainRequestWaits)
            {
                mainRequestWaits[tID] = new AutoResetEvent(false);
            }
            lock (ImmunityHandler_Patch.immunityInfoLists)
            {
                ImmunityHandler_Patch.immunityInfoLists[tID] = new List<ImmunityInfo>();
            }
            lock (RegionListersUpdater_Patch.tmpRegionsLists)
            {
                RegionListersUpdater_Patch.tmpRegionsLists[tID] = new List<Region>();
            }
            lock (PathFinder_Patch.calcGrids)
            {
                PathFinder_Patch.calcGrids[tID] = new PathFinder_Patch.PathFinderNodeFast[0];
            }
            lock (PathFinder_Patch.openLists)
            {
                PathFinder_Patch.openLists[tID] = new FastPriorityQueue<PathFinder_Patch.CostNode2>(new PathFinder_Patch.CostNodeComparer2());
            }
            lock (PathFinder_Patch.openValues)
            {
                PathFinder_Patch.openValues[tID] = 1;
            }
            lock (PathFinder_Patch.closedValues)
            {
                PathFinder_Patch.closedValues[tID] = 2;
            }
            lock (ThingOwnerUtility_Patch.tmpHoldersDict)
            {
                ThingOwnerUtility_Patch.tmpHoldersDict[tID] = new List<IThingHolder>();
            }
            thread.Start();
        }

        private static void ProcessTicks()
        {
            int tID = Thread.CurrentThread.ManagedThreadId;
            eventWaitStarts.TryGetValue(tID, out EventWaitHandle eventWaitStart);
            eventWaitDones.TryGetValue(tID, out EventWaitHandle eventWaitDone);
            while (true)
            {
                eventWaitStart.WaitOne();
                PrepareWorkLists();
                for (int loopsCompleted = listsFullyProcessed; loopsCompleted < totalPrepsCount; loopsCompleted++)
                {
                    prepEventWaitStarts[loopsCompleted].WaitOne();
                    ExecuteTicks();
                }
                CompletePostWorkLists();
                eventWaitDone.Set();
                //WaitingForAllThreadsToComplete.WaitOne();
            }
        }

        private static void CompletePostWorkLists()
        {
            if (Interlocked.Increment(ref workingOnDateNotifierTick) == 0)
            {
                try
                {
                    Find.DateNotifier.DateNotifierTick();
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                }
            }
            if (Interlocked.Increment(ref workingOnHistoryTick) == 0)
            {
                try
                {
                    Find.History.HistoryTick();
                }
                catch (Exception ex10)
                {
                    Log.Error(ex10.ToString());
                }
            }
            if (Interlocked.Increment(ref workingOnMiscellaneous) == 0)
            {
                try
                {
                    Find.Scenario.TickScenario();
                }
                catch (Exception ex2)
                {
                    Log.Error(ex2.ToString());
                }

                try
                {
                    Find.StoryWatcher.StoryWatcherTick();
                }
                catch (Exception ex4)
                {
                    Log.Error(ex4.ToString());
                }

                try
                {
                    Find.GameEnder.GameEndTick();
                }
                catch (Exception ex5)
                {
                    Log.Error(ex5.ToString());
                }

                try
                {
                    Find.Storyteller.StorytellerTick();
                }
                catch (Exception ex6)
                {
                    Log.Error(ex6.ToString());
                }

                try
                {
                    Find.TaleManager.TaleManagerTick();
                }
                catch (Exception ex7)
                {
                    Log.Error(ex7.ToString());
                }

                try
                {
                    Find.QuestManager.QuestManagerTick();
                }
                catch (Exception ex8)
                {
                    Log.Error(ex8.ToString());
                }

                try
                {
                    Find.World.WorldPostTick();
                }
                catch (Exception ex9)
                {
                    Log.Error(ex9.ToString());
                }

                GameComponentUtility.GameComponentTick();
                try
                {
                    Find.LetterStack.LetterStackTick();
                }
                catch (Exception ex11)
                {
                    Log.Error(ex11.ToString());
                }

                try
                {
                    Find.Autosaver.AutosaverTick();
                }
                catch (Exception ex12)
                {
                    Log.Error(ex12.ToString());
                }

                try
                {
                    FilthMonitor2.FilthMonitorTick();
                }
                catch (Exception ex13)
                {
                    Log.Error(ex13.ToString());
                }
            }
        }

        private static void PrepareWorkLists()
        {
            if (Interlocked.Increment(ref workingOnMapPreTick) == 0)
            {
                List<Map> maps = Find.Maps;
                for (int i = 0; i < maps.Count; i++)
                {
                    maps[i].MapPreTick();
                }
                prepEventWaitStarts[Interlocked.Increment(ref currentPrepsDone)].Set(); //WindManager
            }

            if (Interlocked.Increment(ref workingOnTickListNormal) == 0)
            {
                TickManager_Patch.tickListNormal(currentInstance).Tick();
                prepEventWaitStarts[Interlocked.Increment(ref currentPrepsDone)].Set(); //TickNormal
            }
            if (Interlocked.Increment(ref workingOnTickListRare) == 0)
            {
                TickManager_Patch.tickListRare(currentInstance).Tick();
                prepEventWaitStarts[Interlocked.Increment(ref currentPrepsDone)].Set(); //TickRare
            }
            if (Interlocked.Increment(ref workingOnTickListLong) == 0)
            {
                TickManager_Patch.tickListLong(currentInstance).Tick();
                prepEventWaitStarts[Interlocked.Increment(ref currentPrepsDone)].Set(); //TickLong
            }
            if (Interlocked.Increment(ref workingOnWorldTick) == 0)
            {
                try
                {
                    World world = Find.World;
                    world.worldPawns.WorldPawnsTick();
                    world.factionManager.FactionManagerTick();
                    world.worldObjects.WorldObjectsHolderTick();
                    world.debugDrawer.WorldDebugDrawerTick();
                    world.pathGrid.WorldPathGridTick();
                    WorldComponentUtility.WorldComponentTick(world);
                }
                catch (Exception ex3)
                {
                    Log.Error(ex3.ToString());
                }
                prepEventWaitStarts[Interlocked.Increment(ref currentPrepsDone)].Set(); //WorldPawns
                prepEventWaitStarts[Interlocked.Increment(ref currentPrepsDone)].Set(); //Factions
                prepEventWaitStarts[Interlocked.Increment(ref currentPrepsDone)].Set(); //WorldObjects
                prepEventWaitStarts[Interlocked.Increment(ref currentPrepsDone)].Set(); //WorldComponents
            }
            if (Interlocked.Increment(ref workingOnMapPostTick) == 0)
            {
                List<Map> maps = Find.Maps;
                for (int j = 0; j < maps.Count; j++)
                {
                    maps[j].MapPostTick();
                }
                prepEventWaitStarts[Interlocked.Increment(ref currentPrepsDone)].Set(); //WildPlantSpawner
                prepEventWaitStarts[Interlocked.Increment(ref currentPrepsDone)].Set(); //SteadyEnvironment
                prepEventWaitStarts[Interlocked.Increment(ref currentPrepsDone)].Set(); //PassingShipManagerTick
            }

        }

        private static void ExecuteTicks()
        {
            if (thingListNormalTicks > 0)
            {
                int index = Interlocked.Decrement(ref thingListNormalTicks);
                while (index >= 0)
                {
                    Thing thing = thingListNormal[index];
                    if (!thing.Destroyed)
                    {
                        try
                        {
                            thing.Tick();
                        }
                        catch (Exception ex)
                        {
                            string text = thing.Spawned ? (" (at " + thing.Position + ")") : "";
                            if (Prefs.DevMode)
                            {
                                Log.Error("Exception ticking " + thing.ToStringSafe() + text + ": " + ex);
                            }
                            else
                            {
                                Log.ErrorOnce("Exception ticking " + thing.ToStringSafe() + text + ". Suppressing further errors. Exception: " + ex, thing.thingIDNumber ^ 0x22627165);
                            }
                        }
                    }
                    index = Interlocked.Decrement(ref thingListNormalTicks);
                }
                if (index == -1)
                {
                    Interlocked.Increment(ref listsFullyProcessed);
                }
            }
            else if (thingListRareTicks > 0)
            {
                int index = Interlocked.Decrement(ref thingListRareTicks);
                while (index >= 0)
                {
                    Thing thing = thingListRare[index];
                    if (!thing.Destroyed)
                    {
                        try
                        {
                            thing.TickRare();
                        }
                        catch (Exception ex)
                        {
                            string text = thing.Spawned ? (" (at " + thing.Position + ")") : "";
                            if (Prefs.DevMode)
                            {
                                Log.Error("Exception ticking " + thing.ToStringSafe() + text + ": " + ex);
                            }
                            else
                            {
                                Log.ErrorOnce("Exception ticking " + thing.ToStringSafe() + text + ". Suppressing further errors. Exception: " + ex, thing.thingIDNumber ^ 0x22627165);
                            }
                        }
                    }
                    index = Interlocked.Decrement(ref thingListRareTicks);
                }
                if (index == -1)
                {
                    Interlocked.Increment(ref listsFullyProcessed);
                }
            }
            else if (thingListLongTicks > 0)
            {
                int index = Interlocked.Decrement(ref thingListLongTicks);
                while (index >= 0)
                {
                    Thing thing = thingListLong[index];
                    if (!thing.Destroyed)
                    {
                        try
                        {
                            thing.TickLong();
                        }
                        catch (Exception ex)
                        {
                            string text = thing.Spawned ? (" (at " + thing.Position + ")") : "";
                            if (Prefs.DevMode)
                            {
                                Log.Error("Exception ticking " + thing.ToStringSafe() + text + ": " + ex);
                            }
                            else
                            {
                                Log.ErrorOnce("Exception ticking " + thing.ToStringSafe() + text + ". Suppressing further errors. Exception: " + ex, thing.thingIDNumber ^ 0x22627165);
                            }
                        }
                    }
                    index = Interlocked.Decrement(ref thingListLongTicks);
                }
                if (index == -1)
                {
                    Interlocked.Increment(ref listsFullyProcessed);
                }
            }
            else if (worldPawnsTicks > 0)
            {
                int index = Interlocked.Decrement(ref worldPawnsTicks);
                while (index >= 0)
                {
                    Pawn pawn = worldPawnsAlive[index];
                    try
                    {
                        pawn.Tick();
                    }
                    catch (Exception ex)
                    {
                        Log.ErrorOnce("Exception ticking world pawn " + pawn.ToStringSafe() + ". Suppressing further errors. " + (object)ex, pawn.thingIDNumber ^ 1148571423, false);
                    }
                    try
                    {
                        if (!pawn.Dead && !pawn.Destroyed && (pawn.IsHashIntervalTick(7500) && !pawn.IsCaravanMember()) && !PawnUtility.IsTravelingInTransportPodWorldObject(pawn))
                            TendUtility.DoTend(null, pawn, null);
                    }
                    catch (Exception ex)
                    {
                        Log.ErrorOnce("Exception tending to a world pawn " + pawn.ToStringSafe() + ". Suppressing further errors. " + (object)ex, pawn.thingIDNumber ^ 8765780, false);
                    }
                    index = Interlocked.Decrement(ref worldPawnsTicks);
                }
                if (index == -1)
                {
                    Interlocked.Increment(ref listsFullyProcessed);
                }
            }

            else if (worldObjectsTicks > 0)
            {
                int index = Interlocked.Decrement(ref worldObjectsTicks);
                while (index >= 0)
                {
                    try
                    {
                        worldObjects[index].Tick();
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Exception ticking " + worldObjects[index].ToStringSafe() + ": " + ex);
                    }
                    index = Interlocked.Decrement(ref worldObjectsTicks);
                }
                if (index == -1)
                {
                    Interlocked.Increment(ref listsFullyProcessed);
                }
            }

            else if (steadyEnvironmentEffectsTicks > 0)
            {
                int index = Interlocked.Decrement(ref steadyEnvironmentEffectsTicks);
                while (index >= 0)
                {
                    int cycleIndex = (steadyEnvironmentEffectsCycleIndexOffset - index) % steadyEnvironmentEffectsArea;
                    IntVec3 c = steadyEnvironmentEffectsCellsInRandomOrder.Get(cycleIndex);
                    try
                    {
                        SteadyEnvironmentEffects_Patch.DoCellSteadyEffects(steadyEnvironmentEffectsInstance, c);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Exception ticking steadyEnvironmentEffectsCells " + index.ToStringSafe() + ": " + ex);
                    }
                    //Interlocked.Increment(ref SteadyEnvironmentEffects_Patch.cycleIndex(steadyEnvironmentEffectsInstance));
                    index = Interlocked.Decrement(ref steadyEnvironmentEffectsTicks);
                }
                if (index == -1)
                {
                    Interlocked.Increment(ref listsFullyProcessed);
                }
            }

            else if (plantMaterialsCount > 0)
            {
                int index = Interlocked.Decrement(ref plantMaterialsCount);
                while (index >= 0)
                {
                    try
                    {
                        WindManager_Patch.plantMaterials[index].SetFloat(ShaderPropertyIDs.SwayHead, plantSwayHead);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Exception ticking " + WindManager_Patch.plantMaterials[index].ToStringSafe() + ": " + ex);
                    }
                    index = Interlocked.Decrement(ref plantMaterialsCount);
                }
                if (index == -1)
                {
                    Interlocked.Increment(ref listsFullyProcessed);
                }
            }

            else if (allFactionsTicks > 0)
            {
                int index = Interlocked.Decrement(ref allFactionsTicks);
                while (index >= 0)
                {
                    try
                    {
                        allFactions[index].FactionTick();
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Exception ticking " + allFactions[index].ToStringSafe() + ": " + ex);
                    }
                    index = Interlocked.Decrement(ref allFactionsTicks);
                }
                if (index == -1)
                {
                    Interlocked.Increment(ref listsFullyProcessed);
                }
            }

            else if (WildPlantSpawnerTicks > 0)
            {
                int index = Interlocked.Decrement(ref WildPlantSpawnerTicks);
                while (index >= 0)
                {
                    int cycleIndex = (WildPlantSpawnerCycleIndexOffset - index) % WildPlantSpawnerArea;
                    try
                    {
                        IntVec3 intVec = WildPlantSpawnerCellsInRandomOrder.Get(cycleIndex);

                        if ((WildPlantSpawnerCycleIndexOffset - index) > WildPlantSpawnerArea)
                        {
                            Interlocked.Add(ref DesiredPlants2Tmp1000,
                                1000 * (int)WildPlantSpawner_Patch.GetDesiredPlantsCountAt2(WildPlantSpawnerMap, intVec, intVec, WildPlantSpawnerCurrentPlantDensity));
                            if (intVec.GetTerrain(WildPlantSpawnerMap).fertility > 0f)
                            {
                                Interlocked.Increment(ref FertilityCells2Tmp);
                            }

                            float mtb = WildPlantSpawner_Patch.GoodRoofForCavePlant2(WildPlantSpawnerMap, intVec) ? 130f : WildPlantSpawnerMap.Biome.wildPlantRegrowDays;
                            if (Rand.Chance(WildPlantSpawnerChance) && Rand.MTBEventOccurs(mtb, 60000f, 10000) && WildPlantSpawner_Patch.CanRegrowAt2(WildPlantSpawnerMap, intVec))
                            {
                                WildPlantSpawnerInstance.CheckSpawnWildPlantAt(intVec, WildPlantSpawnerCurrentPlantDensity, DesiredPlantsTmp1000 / 1000.0f);
                            }
                        }
                        else
                        {
                            Interlocked.Add(ref DesiredPlantsTmp1000,
                                1000 * (int)WildPlantSpawner_Patch.GetDesiredPlantsCountAt2(WildPlantSpawnerMap, intVec, intVec, WildPlantSpawnerCurrentPlantDensity));
                            if (intVec.GetTerrain(WildPlantSpawnerMap).fertility > 0f)
                            {
                                Interlocked.Increment(ref FertilityCellsTmp);
                            }

                            float mtb = WildPlantSpawner_Patch.GoodRoofForCavePlant2(WildPlantSpawnerMap, intVec) ? 130f : WildPlantSpawnerMap.Biome.wildPlantRegrowDays;
                            if (Rand.Chance(WildPlantSpawnerChance) && Rand.MTBEventOccurs(mtb, 60000f, 10000) && WildPlantSpawner_Patch.CanRegrowAt2(WildPlantSpawnerMap, intVec))
                            {
                                WildPlantSpawnerInstance.CheckSpawnWildPlantAt(intVec, WildPlantSpawnerCurrentPlantDensity, DesiredPlants);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Exception ticking WildPlantSpawner: " + ex);
                    }
                    index = Interlocked.Decrement(ref WildPlantSpawnerTicks);
                }
                if ((WildPlantSpawnerCycleIndexOffset - index) > WildPlantSpawnerArea)
                {
                    WildPlantSpawner_Patch.calculatedWholeMapNumDesiredPlants(WildPlantSpawnerInstance) = DesiredPlantsTmp1000 / 1000.0f;
                    WildPlantSpawner_Patch.calculatedWholeMapNumDesiredPlantsTmp(WildPlantSpawnerInstance) = DesiredPlants2Tmp1000 / 1000.0f;
                    WildPlantSpawner_Patch.calculatedWholeMapNumNonZeroFertilityCells(WildPlantSpawnerInstance) = FertilityCellsTmp;
                    WildPlantSpawner_Patch.calculatedWholeMapNumNonZeroFertilityCellsTmp(WildPlantSpawnerInstance) = FertilityCells2Tmp;
                }
                else
                {
                    WildPlantSpawner_Patch.calculatedWholeMapNumDesiredPlantsTmp(WildPlantSpawnerInstance) = DesiredPlantsTmp1000 / 1000.0f;
                    WildPlantSpawner_Patch.calculatedWholeMapNumNonZeroFertilityCells(WildPlantSpawnerInstance) = FertilityCellsTmp;
                }
                if (index == -1)
                {
                    Interlocked.Increment(ref listsFullyProcessed);
                }
            }
            else if (TradeShipTicks > 0)
            {
                int index = Interlocked.Decrement(ref TradeShipTicks);
                while (index >= 0)
                {
                    Pawn pawn = TradeShipThings[index] as Pawn;
                    if (pawn != null)
                    {
                        try
                        {
                            pawn.Tick();
                        }
                        catch (Exception ex)
                        {
                            Log.Error("Exception ticking Pawn: " + pawn.ToStringSafe() + " " + ex);
                        }
                        if (pawn.Dead)
                        {
                            lock (TradeShipThings)
                            {
                                TradeShipThings.Remove(pawn);
                            }
                        }
                    }
                    index = Interlocked.Decrement(ref TradeShipTicks);
                }
                if (index == -1)
                {
                    Interlocked.Increment(ref listsFullyProcessed);
                }
            }
            else if (WorldComponentTicks > 0)
            {
                int index = Interlocked.Decrement(ref WorldComponentTicks);
                while (index >= 0)
                {
                    //try
                    //{
                    WorldComponent wc = WorldComponents[index];
                    if (null != wc)
                    {
                        lock (wc)
                        {
                            try
                            {
                                wc.WorldComponentTick();
                            }
                            catch (Exception ex)
                            {
                                Log.Error("Exception ticking World Component: " + wc.ToStringSafe() + ex);
                            }
                        }
                    }
                    //}
                    //catch (Exception ex)
                    //{
                    //    Log.Error(ex.ToString());
                    //}
                    index = Interlocked.Decrement(ref WorldComponentTicks);
                }
                if (index == -1)
                {
                    Interlocked.Increment(ref listsFullyProcessed);
                }
            }

            /*
            while(drawQueue.TryDequeue(out Thing drawThing))
            {
                IntVec3 position = drawThing.Position;
                if ((cellRect.Contains(position) || drawThing.def.drawOffscreen) && (!fogGrid[cellIndices.CellToIndex(position)] || drawThing.def.seeThroughFog) && (drawThing.def.hideAtSnowDepth >= 1.0 || snowGrid.GetDepth(position) <= (double)drawThing.def.hideAtSnowDepth))
                {
                    try
                    {
                        drawThing.Draw();
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Exception drawing " + (object)drawThing + ": " + ex.ToString(), false);
                    }
                }
            }
            */

        }

        private static void MonitorThreads()
        {
            while (true)
            {
                monitorThreadWaitHandle.WaitOne();
                workingOnMapPreTick = -1;
                workingOnTickListNormal = -1;
                workingOnTickListRare = -1;
                workingOnTickListLong = -1;
                workingOnDateNotifierTick = -1;
                workingOnWorldTick = -1;
                workingOnMapPostTick = -1;
                workingOnHistoryTick = -1;
                currentPrepsDone = -1;
                workingOnMiscellaneous = -1;
                listsFullyProcessed = 0;
                foreach (EventWaitHandle eventWaitStart in eventWaitStarts.Values)
                {
                    eventWaitStart.Set();
                }
                stopwatch.Restart();
                foreach (int tID2 in eventWaitDones.Keys.ToList())
                {
                    if (eventWaitDones.TryGetValue(tID2, out EventWaitHandle eventWaitDone))
                    {
                        if (!eventWaitDone.WaitOne(timeoutMS))
                        {
                            //超时豁免线程
                            bool timoutExempt;
                            lock (timeoutExemptThreads2)
                            {
                                timoutExempt = timeoutExemptThreads2.ContainsKey(tID2);
                            }

                            if (!timoutExempt)
                            {
                                Log.Error("Thread: " + tID2.ToString() + " did not finish within " + timeoutMS.ToString() + "ms. Restarting thread...");
                                AbortWorkerThread(tID2);
                                CreateWorkerThread();
                            }
                            else
                            {
                                int timeoutOverride;
                                lock (timeoutExemptThreads2)
                                {
                                    timeoutOverride = timeoutExemptThreads2[tID2];
                                }
                                //等待.如果还有就移除.
                                eventWaitDone.WaitOne(timeoutOverride);
                                lock (timeoutExemptThreads2)
                                {
                                    if (timeoutExemptThreads2.ContainsKey(tID2))
                                    {
                                        timeoutExemptThreads2.Remove(tID2);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Log.Error("Thread monitor cannot find thread: " + tID2.ToString());
                    }
                }
                allWorkerThreadsFinished = true;
                mainThreadWaitHandle.Set();
            }
        }

        private static void AbortWorkerThread(int managedThreadID)
        {
            if (allThreads.TryGetValue(managedThreadID, out Thread thread))
            {
                thread.Abort();
                allThreads.Remove(managedThreadID);
                lock (eventWaitStarts)
                {
                    eventWaitStarts.Remove(managedThreadID);
                }
                lock (eventWaitDones)
                {
                    eventWaitDones.Remove(managedThreadID);
                }
                lock (mainRequestWaits)
                {
                    mainRequestWaits.Remove(managedThreadID);
                }
                lock (ImmunityHandler_Patch.immunityInfoLists)
                {
                    ImmunityHandler_Patch.immunityInfoLists.Remove(managedThreadID);
                }
                lock (RegionListersUpdater_Patch.tmpRegionsLists)
                {
                    RegionListersUpdater_Patch.tmpRegionsLists.Remove(managedThreadID);
                }
                lock (PathFinder_Patch.calcGrids)
                {
                    PathFinder_Patch.calcGrids.Remove(managedThreadID);
                }
                lock (PathFinder_Patch.openLists)
                {
                    PathFinder_Patch.openLists.Remove(managedThreadID);
                }
                lock (PathFinder_Patch.openValues)
                {
                    PathFinder_Patch.openValues.Remove(managedThreadID);
                }
                lock (PathFinder_Patch.closedValues)
                {
                    PathFinder_Patch.closedValues.Remove(managedThreadID);
                }
                lock (ThingOwnerUtility_Patch.tmpHoldersDict)
                {
                    ThingOwnerUtility_Patch.tmpHoldersDict.Remove(managedThreadID);
                }
            }
            else
            {
                Log.Error("Error finding timed out thread: " + managedThreadID.ToString());
            }
        }

        public static void MainThreadWaitLoop()
        {
            RegionAndRoomUpdater_Patch.workingInt = 0;
            allWorkerThreadsFinished = false;
            monitorThreadWaitHandle.Set();

            while (!allWorkerThreadsFinished)
            {
                mainThreadWaitHandle.WaitOne();
                RespondToSafeFunctionRequests();

                while (PlayOneShot.Count > 0)
                {
                    if (PlayOneShot.TryDequeue(out Tuple<SoundDef, SoundInfo> s))
                    {
                        s.Item1.PlayOneShot(s.Item2);
                    }
                }
                while (PlayOneShotCamera.Count > 0)
                {
                    if (PlayOneShotCamera.TryDequeue(out Tuple<SoundDef, Map> s))
                    {
                        s.Item1.PlayOneShotOnCamera(s.Item2);
                    }
                }

            }
        }


        private static void RespondToSafeFunctionRequests()
        {
            while (safeFunctionRequests.Count > 0)
            {
                object[] functionAndParameters;
                int key;
                lock (safeFunctionRequests)
                {
                    key = safeFunctionRequests.Keys.First();
                    functionAndParameters = safeFunctionRequests[key];
                    safeFunctionRequests.Remove(key);
                }
                object result;
                object[] parameters = (object[])functionAndParameters[1];

                if (functionAndParameters[0] is Func<object[], object> safeFunction)
                {
                    result = safeFunction(parameters);
                    safeFunctionResults[key] = result;
                }
                else if (functionAndParameters[0] is Action<object[]> safeAction)
                {
                    safeAction(parameters);
                }
                else
                {
                    Log.Error("First perameter of thread-safe function request was not an action or function");
                }
                if (mainRequestWaits.TryGetValue(key, out EventWaitHandle eventWaitStart))
                    eventWaitStart.Set();
                else
                    Log.Error("Thread " + key.ToString() + " ended during main Thread request.");
            }
        }


    }

}


