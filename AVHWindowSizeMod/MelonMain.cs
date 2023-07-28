using MelonLoader;
using HarmonyLib;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections;
using System.Configuration;
using System.Runtime.InteropServices;
using System.Diagnostics;
using UnityEngine.XR;
using System.Security.Policy;

// When you have a monitor that supports multiple refresh rates your resolution selection box will have doubled entries,
// but the code used by the game automatically selects the highest refresh rate supported no matter which of the duplicate entries you choose.

namespace AVHWindowSizeMod
{
    public class MelonMain : MelonMod
    {
        // Hooking into Windows C++ functions
        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        public static extern IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        // This definition is required, cant use predefined rectangle class
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner
        }

        // Variables
        internal static string modFolder = $"{Environment.CurrentDirectory}\\Mods\\{Assembly.GetExecutingAssembly().GetName().Name}";
        public static List<Resolution> resolutionsList;



        public override void OnInitializeMelon()
        {
            // Minimum visual check by user that the mod is installed. When working properly this is all the user should see.
            LoggerInstance.Msg("Keep Window Size Mod is installed");

            if (!ModConfig.Instance.Read("initialized", out bool testBool)) LoggerInstance.Msg("No saved settings, initializing");

            // Check if the mod has been run before and if so make sure it ran properly
            if (!ModConfig.Instance.Write("initialized", true.ToString())) LoggerInstance.Msg("Config failed to initialize");
        }

        public override void OnDeinitializeMelon() // Destructor
        {
            RECT windowRect = new RECT();
            IntPtr handle = Process.GetCurrentProcess().MainWindowHandle;
            if (handle != IntPtr.Zero)
            {
                // Get the window position and save it to the config file
                GetWindowRect(handle, out windowRect);
                ModConfig.Instance.Write("lastX", windowRect.Left);
                ModConfig.Instance.Write("lastY", windowRect.Top);
            }
        }

        public static void MoveWindow(int x, int y)
        {
            const short SWP_NOSIZE = 1; // Dont resize window, only move
            const short SWP_NOZORDER = 0X4;
            const int SWP_SHOWWINDOW = 0x0040;

            IntPtr handle = Process.GetCurrentProcess().MainWindowHandle;
            if (handle != IntPtr.Zero)
            {
                SetWindowPos(handle, 0, x, y, 0, 0, SWP_NOZORDER | SWP_NOSIZE | SWP_SHOWWINDOW);
            }
        }

        [HarmonyPatch(typeof(ResolutionScript), "Start")]
        public class ResolutionScriptStart_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(ref ResolutionScript __instance)
            {
                resolutionsList = new List<Resolution>();
                List<string> stringList = new List<string>();
                int menuValue = 0, settingsWidth = 0, settingsHeight = 0;
                bool useConfig = false;

                // Load last known window size and only set 'useConfig' if the load is successful
                if (ModConfig.Instance.Read("width", out settingsWidth) && ModConfig.Instance.Read("height", out settingsHeight))
                    useConfig = true;

                // Loop through all resolutions supported by the monitor in fullscreen
                for (int i = 0; i < Screen.resolutions.Length; i++)
                {
                    string item = Screen.resolutions[i].width + "x" + Screen.resolutions[i].height; // Intentionally not using Resolution.ToString() because we don't care about refresh rate
                    string match = stringList.FirstOrDefault(stringToCheck => stringToCheck.Contains(item)); // Look for string in list
                    if (match == null) // Filter out duplicates
                    {
                        if(useConfig) // Protects against corrupt or missing config key/value pairs
                        {
                            // Match to config values and prepare to select this resolution in the menu
                            if (Screen.resolutions[i].width == settingsWidth && Screen.resolutions[i].height == settingsHeight) menuValue = stringList.Count;
                        }
                        else
                        {
                            // Replicate default game behavior - set menu to reflect monitor resolution which the game should have autoresized to on launch
                            if (Screen.resolutions[i].width == Screen.currentResolution.width && Screen.resolutions[i].height == Screen.currentResolution.height) menuValue = stringList.Count;
                        }

                        stringList.Add(item); // Must come after useing stringList.Count or before using stringList.Count - 1 for indexes to line up
                        resolutionsList.Add(Screen.resolutions[i]);
                    }
                }

                // Set the private menu resolutions array native to the game
                Traverse.Create(__instance).Field("resolutions").SetValue(resolutionsList.ToArray());

                // Update Menu
                __instance.resolutionDropdown.ClearOptions();
                __instance.resolutionDropdown.AddOptions(stringList);
                __instance.resolutionDropdown.value = menuValue;
                __instance.resolutionDropdown.RefreshShownValue();

                // Resize Window
                __instance.SetResolution(__instance.resolutionDropdown.value);
                __instance.fullscreenToggle.isOn = Screen.fullScreen;

                // Move window if not in fullscreen
                if (!Screen.fullScreen)
                {
                    int newX, newY;
                    if (ModConfig.Instance.Read("lastX", out newX) && ModConfig.Instance.Read("lastY", out newY))
                    {
                        MoveWindow(newX, newY);
                    }
                }

                return false; // Do not run original function
            }
        }

        [HarmonyPatch(typeof(ResolutionScript), "SetResolution")]
        public class ResolutionScriptSetResolution_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(ref ResolutionScript __instance, int resolutionIndex)
            {
                ModConfig.Instance.Write("width", resolutionsList[resolutionIndex].width);
                ModConfig.Instance.Write("height", resolutionsList[resolutionIndex].height);
            }
        }
    }
}