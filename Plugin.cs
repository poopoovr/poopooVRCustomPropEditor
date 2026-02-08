using System;
using BepInEx;
using BepInEx.Logging;
using ExitGames.Client.Photon;
using poopooVRCustomPropEditor.Data;
using poopooVRCustomPropEditor.Features;
using poopooVRCustomPropEditor.UI;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace poopooVRCustomPropEditor
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }

        public MediaController MediaController { get; private set; }
        public ModChecker ModChecker { get; private set; }
        public PlayerInfo PlayerInfo { get; private set; }
        public PlayerInspector PlayerInspector { get; private set; }
        public MenuManager MenuManager { get; private set; }
        public ModDatabaseFetcher DatabaseFetcher { get; private set; }

        private GameObject modContainer;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Log.LogInfo($"{PluginInfo.Name} v{PluginInfo.Version} is loading...");

            try
            {
                new Harmony(PluginInfo.GUID).PatchAll();
                Log.LogInfo("Harmony patches applied");
            }
            catch (Exception ex)
            {
                Log.LogError($"Harmony patch failed: {ex}");
            }

            GorillaTagger.OnPlayerSpawned(OnGameInitialized);

            try
            {
                Hashtable properties = new Hashtable();
                properties.Add(PluginInfo.GUID, "1.0.0");
                PhotonNetwork.LocalPlayer.SetCustomProperties(properties);
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Could not set custom properties in Awake: {ex.Message}");
            }

            Log.LogInfo($"{PluginInfo.Name} awaiting game initialization...");
        }

        private void OnGameInitialized()
        {
            try
            {
                Log.LogInfo($"{PluginInfo.Name} initializing features...");

                modContainer = new GameObject("poopoovrowned");
                DontDestroyOnLoad(modContainer);

                DatabaseFetcher = modContainer.AddComponent<ModDatabaseFetcher>();
                DatabaseFetcher.FetchData();
                Log.LogInfo("ModDatabaseFetcher started");

                MediaController = modContainer.AddComponent<MediaController>();
                Log.LogInfo("MediaController added");

                ModChecker = modContainer.AddComponent<ModChecker>();
                Log.LogInfo("ModChecker added");

                PlayerInfo = modContainer.AddComponent<PlayerInfo>();
                Log.LogInfo("PlayerInfo added");

                PlayerInspector = modContainer.AddComponent<PlayerInspector>();
                Log.LogInfo("PlayerInspector added");

                MenuManager = modContainer.AddComponent<MenuManager>();
                Log.LogInfo("MenuManager added");

                Log.LogInfo($"{PluginInfo.Name} loaded successfully!");
            }
            catch (Exception ex)
            {
                Log.LogError($"FATAL: Failed during initialization: {ex}");
            }
        }

        private void OnDestroy()
        {
            Log.LogInfo($"{PluginInfo.Name} is unloading...");
        }
    }

    public static class PluginInfo
    {
        public const string GUID = "com.poopoovrowned.gorillatag";
        public const string Name = "poopoovrowned";
        public const string Version = "1.0.0";
    }
}
