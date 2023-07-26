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

// When you have a monitor that supports multiple refresh rates your resolution selection box will have doubled entries,
// but the code used by the game automatically selects the highest refresh rate supported no matter which of the duplicate entries you choose.

namespace AVHWindowSizeMod
{
    public class MelonMain : MelonMod
    {
        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        public static extern IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner
        }

        internal static string modFolder = $"{Environment.CurrentDirectory}\\Mods\\{Assembly.GetExecutingAssembly().GetName().Name}";
        private static Configuration configFile;
        private static KeyValueConfigurationCollection appSettings;
       
        public static List<Resolution> resolutionsList;

        public static MelonLogger.Instance log;


        public override void OnInitializeMelon()
        {
            log = LoggerInstance;

            configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            appSettings = configFile.AppSettings.Settings;

            if (appSettings == null)
            {
                LoggerInstance.Msg("Failed to access config file!");
            }
            else
            {
                LoggerInstance.Msg("Keep Window Size Mod is installed");
            }
        }

        public override void OnDeinitializeMelon() // Destructor
        {
            if (appSettings == null) return;
            RECT windowRect = new RECT();
            IntPtr handle = Process.GetCurrentProcess().MainWindowHandle;
            if (handle != IntPtr.Zero)
            {
                GetWindowRect(handle, out windowRect);
                WriteSetting("lastX", windowRect.Left.ToString());
                WriteSetting("LastY", windowRect.Top.ToString());
            }
        }

        public static void MoveWindow(int x, int y)
        {
            const short SWP_NOSIZE = 1;
            const short SWP_NOZORDER = 0X4;
            const int SWP_SHOWWINDOW = 0x0040;

            IntPtr handle = Process.GetCurrentProcess().MainWindowHandle;
            if (handle != IntPtr.Zero)
            {
                SetWindowPos(handle, 0, x, y, 0, 0, SWP_NOZORDER | SWP_NOSIZE | SWP_SHOWWINDOW);
            }
        }

        public static string ReadSettingString(string key)
        {
            if (appSettings == null) return null;
            if (appSettings[key] != null)
            {
                return appSettings[key].Value;
            }
            else
            {
                return null;
            }
        }

        public static int ReadSettingInt(string key)
        {
            if (appSettings == null) return 0;
            if (appSettings[key] != null)
            {
                return (Int32.Parse(appSettings[key].Value));
            }
            else
            {
                return 0;
            }
        }

        public static void WriteSetting(string key, string value)
        {
            if (appSettings == null) return;
            if (appSettings[key] != null)
            {
                appSettings[key].Value = value;
            }
            else
            {
                appSettings.Add(key, value);
            }
            configFile.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
        }

        [HarmonyPatch(typeof(ResolutionScript), "Start")]
        public class ResolutionScriptStart_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(ref ResolutionScript __instance)
            {
                if (appSettings == null) return true;

                if (appSettings.Count > 0) // We want the game to run as intended on inital startup after installation, so we check for settings
                {
                    resolutionsList = new List<Resolution>();
                    List<string> stringList = new List<string>();
                    int menuValue = 0; // What to set the menu to on start

                    int settingsWidth, settingsHeight;
                    settingsWidth = ReadSettingInt("width");
                    settingsHeight = ReadSettingInt("height");

                    for (int i = 0; i < Screen.resolutions.Length; i++)
                    {
                        string item = Screen.resolutions[i].width + "x" + Screen.resolutions[i].height;
                        string match = stringList.FirstOrDefault(stringToCheck => stringToCheck.Contains(item)); // Look for string in list
                        if (match == null) // Add the option only if its not already listed
                        {
                            if (Screen.resolutions[i].width == settingsWidth && Screen.resolutions[i].height == settingsHeight)
                            {
                                menuValue = stringList.Count;
                            }

                            stringList.Add(item);
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

                    if(!Screen.fullScreen)
                    {
                        if (appSettings["lastX"] != null && appSettings["lastX"] != null)
                        {
                            MoveWindow(ReadSettingInt("lastX"), ReadSettingInt("lastY"));
                        }
                    }

                    return false; // Do not run original function
                }
                else
                {
                    WriteSetting("installed", "true");
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(ResolutionScript), "SetResolution")]
        public class ResolutionScriptSetResolution_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(ref ResolutionScript __instance, int resolutionIndex)
            {
                WriteSetting("width", resolutionsList[resolutionIndex].width.ToString());
                WriteSetting("height", resolutionsList[resolutionIndex].height.ToString());
            }
        }
    }
}