using System;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using UnityEngine;

namespace poopooVRCustomPropEditor.Utils
{
    public static class PhotonHelper
    {
        #region Property Keys

        public const string KEY_MODS = "mods";
        public const string KEY_MOD_LIST = "modList";
        public const string KEY_PLATFORM = "platform";
        public const string KEY_REFRESH_RATE = "refreshRate";
        public const string KEY_HERTZ = "hertz";
        public const string KEY_STEAM_ID = "steamId";
        public const string KEY_OCULUS_ID = "oculusId";
        public const string KEY_PLAYER_NAME = "playerName";
        public const string KEY_COLOR = "color";
        public const string KEY_COSMETICS = "cosmetics";

        #endregion

        #region Connection Status

        public static bool IsInRoom => PhotonNetwork.InRoom;
        public static bool IsConnected => PhotonNetwork.IsConnected;
        public static string CurrentRoomCode => PhotonNetwork.CurrentRoom?.Name ?? "N/A";
        public static Player LocalPlayer => PhotonNetwork.LocalPlayer;

        #endregion

        #region Player List

        public static Player[] GetAllPlayers()
        {
            if (!IsInRoom) return Array.Empty<Player>();
            return PhotonNetwork.PlayerList;
        }

        public static Player[] GetOtherPlayers()
        {
            if (!IsInRoom) return Array.Empty<Player>();
            return PhotonNetwork.PlayerListOthers;
        }

        public static int GetPlayerCount()
        {
            if (!IsInRoom) return 0;
            return PhotonNetwork.CurrentRoom.PlayerCount;
        }

        #endregion

        #region Custom Properties Reading

        public static T GetProperty<T>(Player player, string key, T defaultValue = default)
        {
            if (player == null || player.CustomProperties == null)
                return defaultValue;

            if (player.CustomProperties.TryGetValue(key, out object value))
            {
                if (value is T typedValue)
                    return typedValue;

                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }

            return defaultValue;
        }

        public static string GetStringProperty(Player player, string key, string defaultValue = "")
        {
            return GetProperty(player, key, defaultValue);
        }

        public static int GetIntProperty(Player player, string key, int defaultValue = 0)
        {
            return GetProperty(player, key, defaultValue);
        }

        public static float GetFloatProperty(Player player, string key, float defaultValue = 0f)
        {
            return GetProperty(player, key, defaultValue);
        }

        public static bool GetBoolProperty(Player player, string key, bool defaultValue = false)
        {
            return GetProperty(player, key, defaultValue);
        }

        public static bool HasProperty(Player player, string key)
        {
            return player?.CustomProperties?.ContainsKey(key) ?? false;
        }

        public static Dictionary<string, object> GetAllProperties(Player player)
        {
            var result = new Dictionary<string, object>();

            if (player?.CustomProperties == null)
                return result;

            foreach (var kvp in player.CustomProperties)
            {
                if (kvp.Key is string key)
                {
                    result[key] = kvp.Value;
                }
            }

            return result;
        }

        #endregion

        #region Mod Detection Helpers

        public static List<string> GetPlayerMods(Player player)
        {
            var mods = new List<string>();

            if (player?.CustomProperties == null)
                return mods;

            string[] modKeys = { KEY_MODS, KEY_MOD_LIST, "Mods", "ModList", "modlist" };

            foreach (var key in modKeys)
            {
                if (player.CustomProperties.TryGetValue(key, out object value))
                {
                    if (value is string modString && !string.IsNullOrEmpty(modString))
                    {
                        var modNames = modString.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var mod in modNames)
                        {
                            var trimmed = mod.Trim();
                            if (!string.IsNullOrEmpty(trimmed) && !mods.Contains(trimmed))
                                mods.Add(trimmed);
                        }
                    }
                    else if (value is string[] modArray)
                    {
                        foreach (var mod in modArray)
                        {
                            if (!string.IsNullOrEmpty(mod) && !mods.Contains(mod))
                                mods.Add(mod);
                        }
                    }
                    else if (value is List<string> modList)
                    {
                        foreach (var mod in modList)
                        {
                            if (!string.IsNullOrEmpty(mod) && !mods.Contains(mod))
                                mods.Add(mod);
                        }
                    }
                }
            }

            return mods;
        }

        #endregion

        #region Room Properties

        public static T GetRoomProperty<T>(string key, T defaultValue = default)
        {
            if (!IsInRoom || PhotonNetwork.CurrentRoom.CustomProperties == null)
                return defaultValue;

            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value))
            {
                if (value is T typedValue)
                    return typedValue;

                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }

            return defaultValue;
        }

        #endregion

        #region Debug Helpers

        public static void LogPlayerProperties(Player player)
        {
            if (player?.CustomProperties == null)
            {
                Debug.Log($"[PhotonHelper] Player {player?.NickName ?? "null"} has no custom properties");
                return;
            }

            Debug.Log($"[PhotonHelper] Properties for {player.NickName}:");
            foreach (var kvp in player.CustomProperties)
            {
                Debug.Log($"  {kvp.Key}: {kvp.Value} (Type: {kvp.Value?.GetType().Name ?? "null"})");
            }
        }

        #endregion
    }
}
