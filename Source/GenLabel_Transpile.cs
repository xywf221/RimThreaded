﻿using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Verse;
using System.Reflection.Emit;
using System;
using Verse.AI;
using static HarmonyLib.AccessTools;
using RimWorld;
using System.Reflection;
using System.Threading;

namespace RimThreaded
{
    public class GenLabel_Transpile
    {
        public static IEnumerable<CodeInstruction> ThingLabel(IEnumerable<CodeInstruction> instructions, ILGenerator iLGenerator)
        {
			List<CodeInstruction> instructionsList = instructions.ToList();
            List<CodeInstruction> loadLockObjectInstructions = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldsfld, Field(typeof(GenLabel), "labelDictionary"))
            };
			Type labelRequest = TypeByName("RimWorld.GenLabel+LabelRequest");
			Type lockObjectType = typeof(Dictionary<,>).MakeGenericType(new Type[] { labelRequest, typeof(string) });
			LocalBuilder lockObject = iLGenerator.DeclareLocal(lockObjectType);
			LocalBuilder lockTaken = iLGenerator.DeclareLocal(typeof(bool));
			int currentInstructionIndex = 0;
			int matchesFound = 0;

			while (currentInstructionIndex < instructionsList.Count)
			{
				if (currentInstructionIndex + 2 < instructionsList.Count && 
					instructionsList[currentInstructionIndex].opcode == OpCodes.Ldsfld &&
					(FieldInfo)instructionsList[currentInstructionIndex].operand == Field(typeof(GenLabel), "labelDictionary") &&
					instructionsList[currentInstructionIndex + 2].opcode == OpCodes.Ldloca_S
					)
				{
					matchesFound++;
					loadLockObjectInstructions[0].labels = instructionsList[currentInstructionIndex].labels;
					for (int i = 0; i < loadLockObjectInstructions.Count; i++)
					{
						yield return(loadLockObjectInstructions[i]);
					}
					instructionsList[currentInstructionIndex].labels = new List<Label>();
					yield return (new CodeInstruction(OpCodes.Stloc, lockObject.LocalIndex));
					yield return (new CodeInstruction(OpCodes.Ldc_I4_0));
					yield return (new CodeInstruction(OpCodes.Stloc, lockTaken.LocalIndex));
					CodeInstruction codeInstruction = new CodeInstruction(OpCodes.Ldloc, lockObject.LocalIndex);
					codeInstruction.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
					yield return (codeInstruction);
					yield return (new CodeInstruction(OpCodes.Ldloca_S, lockTaken.LocalIndex));
					yield return (new CodeInstruction(OpCodes.Call, Method(typeof(Monitor), "Enter",
						new Type[] { typeof(object), typeof(bool).MakeByRefType() })));
					while(currentInstructionIndex < instructionsList.Count && 
						instructionsList[currentInstructionIndex - 1].opcode != OpCodes.Callvirt || 
						(MethodInfo)instructionsList[currentInstructionIndex - 1].operand != Method(lockObjectType, "Add"))
					{
						yield return (instructionsList[currentInstructionIndex]);
						currentInstructionIndex++;
					}
					Label endHandlerDestination = iLGenerator.DefineLabel();
					yield return (new CodeInstruction(OpCodes.Leave_S, endHandlerDestination));
					codeInstruction = new CodeInstruction(OpCodes.Ldloc, lockTaken.LocalIndex);
					codeInstruction.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginFinallyBlock));
					yield return (codeInstruction);
					Label endFinallyDestination = iLGenerator.DefineLabel();
					yield return (new CodeInstruction(OpCodes.Brfalse_S, endFinallyDestination));
					yield return (new CodeInstruction(OpCodes.Ldloc, lockObject.LocalIndex));
					yield return (new CodeInstruction(OpCodes.Call, Method(typeof(Monitor), "Exit")));
					codeInstruction = new CodeInstruction(OpCodes.Endfinally);
					codeInstruction.labels.Add(endFinallyDestination);
					codeInstruction.blocks.Add(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock));
					yield return (codeInstruction);
					instructionsList[currentInstructionIndex].labels.Add(endHandlerDestination);
				}
				else
				{
					yield return instructionsList[currentInstructionIndex];
					currentInstructionIndex++;
				}
			}
			if (matchesFound < 1)
			{
				Log.Error("IL code instructions not found");
			}
		}      
    }
}
