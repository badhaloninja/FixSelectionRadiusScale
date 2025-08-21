using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace FixSelectionRadiusScale
{
    public class FixSelectionRadiusScale : ResoniteMod
    {
        public override string Name => "FixSelectionRadiusScale";
        public override string Author => "badhaloninja";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/badhaloninja/FixSelectionRadiusScale";

        public override void OnEngineInit()
        {
            Harmony harmony = new("ninja.badhalo.FixSelectionRadiusScale");
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(DevTool))]
        class DevToolPatch
        {
            static readonly MethodInfo slotGlobalPositionGetter = typeof(Slot).GetMethod("get_GlobalPosition", BindingFlags.Instance | BindingFlags.Public);
            static readonly MethodInfo getScaledRadiusMethod = SymbolExtensions.GetMethodInfo(() => GetScaledRadius(null));

            [HarmonyDebug]
            [HarmonyTranspiler]
            [HarmonyPatch("TryOpenGizmo")]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Msg("Slot.GlobalPosition.get: " + slotGlobalPositionGetter);

                // Find local index for enumerated slot
                //
                // Find our 0.1f range value and replace with
                //   load up our slot value onto the stack
                //   call our get scale function with slot as the input and store onto the stack

                // TODO Rework to fori loop
                // TODO Compensate for sphere collision check as well

                var found = false;

                int previousLocal = -1;
                bool foundSlotLocal = false;

                OpCode previousOpcode = OpCodes.Nop;
                foreach (var instruction in instructions)
                {
                    // Find the local index for the curren't enumerated slot
                    if (!foundSlotLocal)
                    {
                        // Store the last used local index
                        if (instruction.IsLdloc())
                        {
                            previousLocal = instruction.LocalIndex();
                        } 
                        else if (instruction.Calls(slotGlobalPositionGetter))
                        {
                            // Mark the previous local as our slot variable
                            Msg("Found Slot.GlobalPosition.get the used local is: " + previousLocal);
                            foundSlotLocal = true;
                        }
                    }

                    // Should have found the slot at this point
                    // Check if we've found the correct 0.1f assignment
                    if (foundSlotLocal && instruction.Is(OpCodes.Ldc_R4, 0.1f) && previousOpcode == OpCodes.Ldloc_S)
                    {
                        // Replace 0.1f with getScaledRadiusMethod(slot)

                        // Load slot on to the stack
                        yield return CodeInstruction.LoadLocal(previousLocal);
                        // Load the value from call onto the stack
                        yield return new CodeInstruction(OpCodes.Call, getScaledRadiusMethod);
                        found = true;

                        // Prevent writing 0.1f in the output
                        continue;
                    }

                    // Write the original instruction
                    yield return instruction;
                    // Store the previous instruction's opcode
                    previousOpcode = instruction.opcode;
                }
                if (found is false)
                {
                    Error("Cannot apply patch, something was not found!");

                    if (foundSlotLocal)
                    {
                        Error("    Unable to find the slot local");
                    }
                }
            }

            public static float GetScaledRadius(Slot slot) =>
                slot.LocalUserRoot.GlobalScale * DevTool.GIZMO_SEARCH_RADIUS;


        }
    }
}