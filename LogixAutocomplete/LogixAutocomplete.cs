using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using FrooxEngine.LogiX;
using System;
using System.Linq;
using ReflectionHelper;
using FrooxEngine.LogiX.ProgramFlow;
using System.Collections.Generic;
using System.Reflection.Emit;
using SpecialItemsLib;

namespace LogixAutocomplete
{
    public class LogixAutocomplete : NeosMod
    {
        public override string Name => DYN_SPACE;
        public override string Author => "art0007i";
        public override string Version => "0.2.0";
        public override string Link => "https://github.com/art0007i/LogixAutocomplete/";

        [AutoRegisterConfigKey]
        public static ModConfigurationKey<bool> KEY_ENABLED = new("enabled", "If true the mod will be enabled.", () => true);
        [AutoRegisterConfigKey]
        public static ModConfigurationKey<bool> KEY_BLOCK_MOVEMENT = new("block_movement", "If true movement will be blocked while the selection dialog is open.", () => true);
        public static ModConfiguration config;
        public override void OnEngineInit()
        {
            config = GetConfiguration();
            item = SpecialItemsLib.SpecialItemsLib.RegisterItem("autocomplete_dialog");
            Harmony harmony = new Harmony("me.art0007i.LogixAutocomplete");
            harmony.PatchAll();

        }

        const string AUTOCOMPLETE_DIALOG_TAG = "autocomplete_dialog";
        const string DYN_SPACE = "LogixAutocomplete";

        public static CustomSpecialItem item;
        public static Slot currentAutocompletePanel;
        public static bool runDefault = false;

        [HarmonyPatch(typeof(SlotHelper), "GenerateTags", new Type[] { typeof(Slot), typeof(HashSet<string>) })]
        class SlotHelper_GenerateTags_Patch
        {
            static void Postfix(Slot slot, HashSet<string> tags)
            {
                // usually you only need to change which component you are looking for
                // but you can change up this entire method in case you have a more complex
                // algorithm for figuring out if an item should be favoritable
                var dynspace = slot.GetComponent<DynamicVariableSpace>();
                if (dynspace != null && dynspace.SpaceName == DYN_SPACE)
                {
                    tags.Add(AUTOCOMPLETE_DIALOG_TAG);
                }
            }
        }

        [HarmonyPatch(typeof(LogixTip))]
        class LogixAutocompleteSpawningPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch("OnSecondaryPress")]
            public static bool SecondaryPrefix(LogixTip __instance,
                IInputElement ____input,
                IWorldElement ____output,
                Action ____impulseTarget,
                Impulse ____impulseSource,
                Slot ____tempWire)
            {
                if (runDefault || !config.GetValue(KEY_ENABLED)) return true;

                if (!ShouldntBlockInputs()) { return false; }

                // IDK what to call this type
                // it basically keeps track of what kind of logix wire you have (input, output, etc.) and what type it is (float, int, etc.)
                IOPair ourPair;
                if (____input != null) ourPair = new IOPair(IOType.ValueInput, ____input.InputType);
                else if (____output != null) ourPair = new IOPair(IOType.ValueOutput, LogixHelper.GetOutputContentType(____output));
                else if (____impulseTarget != null) ourPair = new IOPair(IOType.ImpulseInput, typeof(Action));
                else if (____impulseSource != null) ourPair = new IOPair(IOType.ImpulseOutput, typeof(Action));
                else return true;

                __instance.StartTask(async () =>
                {

                    Slot newSlot = __instance.World.LocalUserSpace.AddSlot(DYN_SPACE);
                    CoroutineManager.Manager.Value = __instance.World.Coroutines;
                    await (default(ToBackground));
                    await newSlot.LoadObjectAsync(item.Uri ?? new Uri("neosdb:///945fa3c9ffc64ec6b61d04dcd1955b059ca5a6ef7a6801ec4e1e4004bc30003d.7zbson"));
                    await (default(ToWorld));
                    var component = newSlot.GetComponent<InventoryItem>();
                    newSlot = ((component != null) ? component.Unpack(null) : null) ?? newSlot;
                    newSlot.PersistentSelf = false;
                    var mySpace = newSlot.GetComponent<DynamicVariableSpace>((p) => p.SpaceName == DYN_SPACE);
                    if (mySpace == null)
                    {
                        Error($"Dynamic Variable Space '{DYN_SPACE}' Not found on item. Make sure you have not saved a holder!");
                        newSlot.Destroy();
                        return;
                    }
                    if (!mySpace.TryWriteValue("LogixTip", __instance))
                    {
                        // maybe there is a way to store the logix tip better than in a dynvar but idk for now it works
                        Error("Failed to write to 'LogixTip' variable! It is required for the mod to work.");
                        newSlot.Destroy();
                        return;
                    }

                    newSlot.OnPrepareDestroy += (sl) =>
                    {
                        if (____tempWire != null)
                        {
                            ____tempWire.Destroy();
                        }
                        __instance.QuickSetField<IInputElement>("_input", null);
                        __instance.QuickSetField<IWorldElement>("_output", null);
                        __instance.QuickSetField<Action>("_impulseTarget", null);
                        __instance.QuickSetField<Impulse>("_impulseSource", null);
                    };
                    __instance.QuickCall("PositionSpawnedNode", new object[] { newSlot });
                    currentAutocompletePanel = newSlot;

                    // TODO: CHANGE THIS TO ACTUALLY GET THE LIST OF TYPES DYNAMICALLY LOL (ALSO GOOD LUCK)
                    IEnumerable<Type> possibleInputs = new Type[] { null };
                    if (Constants.nodeLookup.TryGetValue(ourPair, out var typeList)) possibleInputs = possibleInputs.Concat(typeList);

                    var wireTarget = mySpace.GetManager<Slot>("WireTarget", true).Value;
                    if (wireTarget != null)
                    {
                        ____tempWire.GetComponent<ConnectionWire>().TargetSlot.Target = wireTarget;
                    }
                    var wireType = mySpace.GetManager<int>("WireType", true);
                    if (wireType != null)
                    {
                        Msg(ourPair.wireType);
                        Msg((int)ourPair.ioType);
                        wireType.SetValue((int)ourPair.ioType);
                    }

                    var selectedOption = mySpace.GetManager<SyncType>("SelectedOption", true).Value;
                    if (selectedOption != null) selectedOption.Value = null;

                    var listRoot = mySpace.GetManager<Slot>("ListRoot", true).Value;
                    if (listRoot != null)
                    {
                        var template = mySpace.GetManager<Slot>("TemplateSlot", true).Value;
                        if (template == null)
                        {
                            Error("Found a 'ListRoot' variable, but not a 'TemplateSlot' variable!");
                        }
                        else
                        {
                            foreach (var input in possibleInputs)
                            {
                                var dupe = template.Duplicate(listRoot);
                                var dupedSpace = dupe.GetComponent<DynamicVariableSpace>((p) => p.SpaceName == "AutocompleteTemplate");
                                dupedSpace.QuickCall("UpdateName");
                                dupedSpace.QuickCall("RelinkAllVariables");

                                dupedSpace.GetManager<string>("String", true).SetValue(input == null ? "_Default_" : LogixHelper.GetNodeName(input));
                                dupedSpace.GetManager<SyncType>("Type", true).Value.Value = input;
                            }
                        }
                    }


                    if (config.GetValue(KEY_BLOCK_MOVEMENT))
                    {
                        __instance.LocalUser.Root.GetRegisteredComponent<LocomotionController>().SupressSources.Add(newSlot.GetComponentInChildren<Component>((c) => true));
                    }
                });
                return false;
            }
            [HarmonyPrefix]
            [HarmonyPatch("OnPrimaryRelease")]
            public static bool ReleasePrefix(LogixTip __instance) => ShouldntBlockInputs();
            [HarmonyPrefix]
            [HarmonyPatch("OnPrimaryPress")]
            public static bool PressPrefix(LogixTip __instance) => ShouldntBlockInputs();

            public static bool ShouldntBlockInputs()
            {
                if (currentAutocompletePanel != null)
                {
                    return currentAutocompletePanel.IsDisposed;
                }
                return true;
            }

            [HarmonyPrefix]
            [HarmonyPatch("PositionSpawnedNode")]
            public static bool NodePositionPatch(Slot node)
            {
                if (currentAutocompletePanel == null) return true;
                var mySpace = currentAutocompletePanel.FindSpace(DYN_SPACE);
                if (mySpace == null) return true;
                var targetSlot = mySpace.GetManager<Slot>("SpawnPoint", true).Value;
                if (targetSlot == null) return true;

                node.GlobalPosition = targetSlot.GlobalPosition;
                node.GlobalRotation = targetSlot.GlobalRotation;
                node.GlobalScale = targetSlot.GlobalScale;
                
                return false;
            }
        }

        [HarmonyPatch(typeof(DynamicImpulseTrigger), "Run")]
        class DynamicImpulseCapture
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
            {
                foreach (var code in codes)
                {
                    yield return code;
                    if (code.Calls(AccessTools.Method(typeof(Input<string>), "Evaluate")))
                    {
                        yield return new CodeInstruction(OpCodes.Dup);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DynamicImpulseCapture), nameof(ImpulsePatch)));
                    }
                }
            }

            public static void ImpulsePatch(string str)
            {
                if (str != DYN_SPACE) return;

                if (currentAutocompletePanel == null) return;

                var dynSpace = currentAutocompletePanel.FindSpace(DYN_SPACE);
                if (dynSpace == null) return;

                var syncType = dynSpace.GetManager<SyncType>("SelectedOption", true).Value;
                if (syncType == null) return;

                var tip = dynSpace.GetManager<LogixTip>("LogixTip", true).Value;
                if (tip == null) return;

                if (syncType.Value == null)
                {
                    runDefault = true;
                    try
                    {
                        tip.QuickCall("OnSecondaryPress");
                    }
                    finally
                    {
                        runDefault = false;
                        currentAutocompletePanel.Destroy();
                    }
                }
                else
                {
                    var newNode = tip.QuickCall<Slot>("CreateNewNodeSlot", new object[] { LogixHelper.GetNodeName(syncType.Value) });
                    var nodeComp = newNode.AttachComponent(syncType.Value) as LogixNode;
                    var input = tip.QuickGetField<IInputElement>("_input");
                    if (input != null)
                    {
                        var outp = ((IEnumerable<IOutputElement>)AccessTools.Property(typeof(LogixNode), "Outputs").GetValue(nodeComp))
                            .First((outp) => outp.OutputType == input.InputType);
                        input.TryConnectTo(outp);
                        goto end;
                    }
                    var output = tip.QuickGetField<IWorldElement>("_output");
                    if(output != null)
                    {
                        ((IEnumerable<IInputElement>)AccessTools.Property(typeof(LogixNode), "Inputs").GetValue(nodeComp))
                            .First((inp)=> inp.InputType == LogixHelper.GetOutputContentType(output)).TryConnectTo(output);
                        nodeComp.GenerateVisual();
                        goto end;
                    }
                    var impulseInput = tip.QuickGetField<Action>("_impulseTarget");
                    if(impulseInput != null)
                    {
                        ((IEnumerable<Impulse>)AccessTools.Property(typeof(LogixNode), "ImpulseSources").GetValue(nodeComp))
                            .First().Target = impulseInput;
                        nodeComp.GenerateVisual();
                        goto end;
                    }
                    var impulseOutput = tip.QuickGetField<Impulse>("_impulseSource");
                    if(impulseOutput != null)
                    {
                        var imIn = ((IEnumerable<ImpulseTargetInfo>)AccessTools.Property(typeof(LogixNode), "ImpulseTargets").GetValue(nodeComp))
                            .First();
                        impulseOutput.Target = imIn.Method;
                        goto end; // useful goto
                    }
                    end:
                    currentAutocompletePanel.Destroy();
                }
            }
        }
    }
}