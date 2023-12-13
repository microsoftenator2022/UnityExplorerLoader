using HarmonyLib;

using Kingmaker;

using System.IO;
using System.Linq;
using System.Reflection;

using UnityEngine;
using UnityEngine.UI;

using UnityExplorer;
using UnityExplorer.UI;

using UnityModManagerNet;

namespace UnityExplorerLoader
{
    static class Main
    {
        [HarmonyPatch]
        class InputFocusHandler : MonoBehaviour
        {
            public InputField component;
            bool keyboardWasDisabled;

            void Update()
            {
                if (component.isFocused)
                {
                    if (!Game.Instance.Keyboard.Disabled)
                    {
#if DEBUG
                        Main.Logger.Log("Disabling keyboard");
#endif
                        keyboardWasDisabled = true;
                        Game.Instance.Keyboard.Disabled.SetValue(true);
                    }
                    return;
                }

                if (keyboardWasDisabled && !component.isFocused)
                {
#if DEBUG
                    Main.Logger.Log("Enabling keyboard");
#endif
                    keyboardWasDisabled = false;
                    Game.Instance.Keyboard.Disabled.SetValue(false);
                }
            }

            [HarmonyPatch(typeof(UIFactory), nameof(UIFactory.CreateInputField))]
            [HarmonyPostfix]
            static InputFieldRef UIFactory_CreateInputField_Postfix(InputFieldRef __return)
            {
                __return.Component.gameObject.AddComponent<InputFocusHandler>().component = __return.Component;

                return __return;
            }
        }

        [HarmonyPatch]
        static class Loader
        {
            [HarmonyPatch(typeof(GameMainMenu), "Awake")]
            [HarmonyPostfix]
            public static void MainMenu_Awake()
            {
                Main.Logger.Log("Instantiating UnityExplorer");
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

        internal static Harmony HarmonyInstance;
        internal static UnityModManager.ModEntry.ModLogger Logger;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            Logger = modEntry.Logger;
            modEntry.OnUnload = OnUnload;
            modEntry.OnGUI = OnGUI;
            HarmonyInstance = new Harmony(modEntry.Info.Id);

            Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "UnityExplorer", "UnityExplorer.STANDALONE.Mono.dll"));

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
