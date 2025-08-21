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
            static readonly MethodInfo componentSlotGetter = typeof(Component).GetMethod("get_Slot", BindingFlags.Instance | BindingFlags.Public);
            static readonly MethodInfo getScaledRadiusMethod = SymbolExtensions.GetMethodInfo(() => GetScaledRadius(0, null));

            static readonly float COLLIDER_SEARCH_RADIUS = 0.025f;

            [HarmonyDebug]
            [HarmonyTranspiler]
            [HarmonyPatch("TryOpenGizmo")]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Msg("Slot.GlobalPosition.get: " + componentSlotGetter);

                // Find local index for enumerated slot
                //
                // Find our 0.1f range value and replace with
                //   load up our slot value onto the stack
                //   call our get scale function with slot as the input and store onto the stack

                var foundSlotCheck = false;
                var foundColliderCheck = false;

                int? foundUserSlotLocal = null;

                CodeInstruction previousInstruction = null;
                foreach (var instruction in instructions)
                {
                    // Find the local index for the current enumerated slot
                    if (!foundUserSlotLocal.HasValue)
                    {
                        // Store the last used local index
                        if (instruction.IsStloc() && previousInstruction.Calls(componentSlotGetter))
                        {
                            // Mark the previous local as our slot variable
                            foundUserSlotLocal = instruction.LocalIndex();
                            Msg("Found Slot.GlobalPosition.get the used local is: " + foundUserSlotLocal.Value);
                        }
                    }

                    // Write the original instruction
                    yield return instruction;

                    // Should have found the slot at this point
                    // Check if we've found the correct 0.1f assignment
                    if (foundUserSlotLocal.HasValue && (!foundSlotCheck || !foundColliderCheck))
                    {
                        // Nearest slot radius
                        if ((instruction.Is(OpCodes.Ldc_R4, DevTool.GIZMO_SEARCH_RADIUS) && previousInstruction.opcode == OpCodes.Ldloc_S) || instruction.Is(OpCodes.Ldc_R4, COLLIDER_SEARCH_RADIUS))
                        {
                            // Keep original value and let it be consumed as the first argument for GetScaledRadius(float, slot)

                            // Load slot on to the stack
                            yield return CodeInstruction.LoadLocal(foundUserSlotLocal.Value);
                            // Load the value from call onto the stack
                            yield return new CodeInstruction(OpCodes.Call, getScaledRadiusMethod);

                            if (instruction.Is(OpCodes.Ldc_R4, COLLIDER_SEARCH_RADIUS)) 
                                foundColliderCheck = true;
                            else
                                foundSlotCheck = true;
                        }
                    }

                    previousInstruction = instruction;
                }

                if (!(foundSlotCheck && foundColliderCheck))
                {
                    Error("Cannot apply patch, something was not found!");

                    if (!foundUserSlotLocal.HasValue)
                    {
                        Error("    Unable to find the user root slot local");
                    }
                    if (!foundSlotCheck)
                    {
                        Error("    Unable to find the slot radius check");
                    }
                    if (!foundColliderCheck)
                    {
                        Error("    Unable to find the collider radius check");
                    }
                }
            }

            public static float GetScaledRadius(float value, Slot slot) =>
                slot.LocalUserRoot.GlobalScale * value; //DevTool.GIZMO_SEARCH_RADIUS;


        }
    }
}