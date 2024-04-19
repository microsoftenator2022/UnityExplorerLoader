using HarmonyLib;

using Kingmaker;
using Kingmaker.UI.InputSystems;
using Kingmaker.UI.Selection;

using MicroUtils.Transpiler;

using Owlcat.Runtime.Core.Logging;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using UnityExplorer;
using UnityExplorer.UI;

using UnityModManagerNet;

namespace UnityExplorerLoader
{
    //    [HarmonyPatch]
    //    class InputFocusHandler : MonoBehaviour
    //    {
    //        public InputField component;
    //        bool keyboardWasDisabled;

    //        void Update()
    //        {
    //            if (component.isFocused)
    //            {
    //                if (!Game.Instance.Keyboard.Disabled)
    //                {
    ////#if DEBUG
    ////                    Main.Logger.Log("Disabling keyboard");
    ////#endif
    //                    keyboardWasDisabled = true;
    //                    Game.Instance.Keyboard.Disabled.SetValue(true);
    //                }
    //                return;
    //            }

    //            if (Game.Instance.Keyboard.Disabled && !component.isFocused && keyboardWasDisabled)
    //            {
    ////#if DEBUG
    ////                Main.Logger.Log("Enabling keyboard");
    ////#endif
    //                keyboardWasDisabled = false;
    //                Game.Instance.Keyboard.Disabled.SetValue(false);
    //            }
    //        }

    //        [HarmonyPatch(typeof(UIFactory), nameof(UIFactory.CreateInputField))]
    //        [HarmonyPostfix]
    //        static InputFieldRef UIFactory_CreateInputField_Postfix(InputFieldRef __return)
    //        {
    //            __return.Component.gameObject.AddComponent<InputFocusHandler>().component = __return.Component;

    //            return __return;
    //        }
    //    }

    [HarmonyPatch(typeof(KeyboardAccess))]
    static class KeyboardAccessPatch
    {
        [HarmonyPatch(nameof(KeyboardAccess.IsInputFieldSelected)), HarmonyPrefix]
        static bool Prefix(ref bool __result)
        {
            try
            {
                EventSystem current = EventSystem.current;
                if (current == null)
                    return false;
                GameObject selectedGameObject = current.currentSelectedGameObject;
                __result = selectedGameObject != null && selectedGameObject.GetComponent<InputField>() != null;
                return !__result;
            }
            finally
            {
            }
        }
    }

    [HarmonyPatch]
    class UnityExplorerLoader : MonoBehaviour
    {
        [HarmonyPatch(typeof(GameMainMenu), "Awake")]
        [HarmonyPostfix]
        public static void MainMenu_Awake(GameMainMenu __instance)
        {
            ExplorerStandalone.CreateInstance(delegate (string msg, LogType logType)
            {
                switch (logType)
                {
                    case LogType.Error:
                        Main.Logger.Error(msg);
                        break;
                    case LogType.Assert:
                        Main.Logger.Critical(msg);
                        break;
                    case LogType.Warning:
                        Main.Logger.Warning(msg);
                        break;
                    case LogType.Log:
                        Main.Logger.Log(msg);
                        break;
                    case LogType.Exception:
                        Main.Logger.Error(msg);
                        break;
                }
            });
        }
    }

    static class Main
    {
        internal static Harmony HarmonyInstance;
        internal static UnityModManager.ModEntry.ModLogger Logger;

        internal static ExplorerStandalone UnitExplorer;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            Logger = modEntry.Logger;
            modEntry.OnUnload = OnUnload;
            modEntry.OnGUI = OnGUI;
            HarmonyInstance = new Harmony(modEntry.Info.Id);
            
            var uePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "UnityExplorer.STANDALONE.Mono.dll");
            
            Logger.Log($"Loading Unity Explorer from {uePath}");

            Assembly.LoadFrom(uePath);

            HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry) { }

        static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            HarmonyInstance.UnpatchAll(modEntry.Info.Id);
            return true;
        }
    }
}

[HarmonyPatch]
static class AnnoyingMessagesPatch
{
    static MethodBase TargetMethod() =>
    typeof(KingmakerInputModule)
        .GetNestedTypes(AccessTools.all)
        .Where(t => t.GetCustomAttributes<CompilerGeneratedAttribute>().Any())
        .Select(t => t.GetMethod("MoveNext", AccessTools.all))
        .FirstOrDefault();

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> CheckEventSystem_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var matchPattern = new Func<CodeInstruction, bool>[]
        {
            ci => ci.opcode == OpCodes.Ldsfld,
            ci => ci.opcode == OpCodes.Ldstr,
            ci => ci.Calls(AccessTools.Method(typeof(LogChannel), nameof(LogChannel.Log), [typeof(string)]))
        };

        bool replaceWithNop(IEnumerable<CodeInstruction> instructions)
        {
            var match = instructions.FindInstructionsIndexed(matchPattern);

            if (match.Count() != 3)
                return false;

            foreach (var (_, instruction) in match)
            {
                instruction.opcode = OpCodes.Nop;
                instruction.operand = null;
            }

            return true;
        }

        while (replaceWithNop(instructions)) { }

        return instructions;
    }
}