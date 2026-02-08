using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using poopooVRCustomPropEditor.Data;
using poopooVRCustomPropEditor.Utils;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace poopooVRCustomPropEditor.Features
{
    public class ModCheckResult
    {
        public VRRig Rig { get; set; }
        public Player Player { get; set; }
        public string PlayerName { get; set; }
        public List<string> AllMods { get; set; } = new List<string>();
        public List<string> LegalMods { get; set; } = new List<string>();
        public List<string> IllegalMods { get; set; } = new List<string>();
        public List<string> UnknownMods { get; set; } = new List<string>();

        public bool HasIllegalMods => IllegalMods.Count > 0;
        public bool HasMods => AllMods.Count > 0;

        public Color StatusColor
        {
            get
            {
                if (HasIllegalMods)
                    return Color.red;
                if (LegalMods.Count > 0 && UnknownMods.Count == 0)
                    return Color.green;
                if (UnknownMods.Count > 0)
                    return Color.yellow;
                return Color.white;
            }
        }

        public string StatusText
        {
            get
            {
                if (HasIllegalMods)
                    return $"ILLEGAL ({IllegalMods.Count})";
                if (LegalMods.Count > 0)
                    return $"Legal ({LegalMods.Count})";
                if (UnknownMods.Count > 0)
                    return $"Unknown ({UnknownMods.Count})";
                return "No Mods";
            }
        }

        public string GetFormattedModsList()
        {
            var parts = new List<string>();
            
            foreach (var mod in IllegalMods)
                parts.Add($"[<color=red>{mod}</color>]");
            
            foreach (var mod in LegalMods)
                parts.Add($"[<color=green>{mod}</color>]");
            
            foreach (var mod in UnknownMods)
                parts.Add($"[<color=yellow>{mod}</color>]");
            
            return string.Join(" ", parts);
        }
    }

    public class ModChecker : MonoBehaviour
    {
        #region Properties

        public Dictionary<VRRig, ModCheckResult> PlayerModResults { get; private set; } = new Dictionary<VRRig, ModCheckResult>();
        public bool AutoCheckEnabled { get; set; } = true;
        public float CheckInterval { get; set; } = 5f;
        public event Action<ModCheckResult> OnIllegalModDetected;
        private HashSet<string> CheckedPlayers { get; set; } = new HashSet<string>();
        private string currentRoom;

        #endregion

        #region Private Fields

        private float lastCheckTime;

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            if (!AutoCheckEnabled || !PhotonHelper.IsInRoom)
                return;

            SyncLobbyTracking();

            if (Time.time - lastCheckTime >= CheckInterval)
            {
                CheckAllPlayers();
                lastCheckTime = Time.time;
            }
        }

        #endregion

        #region Public Methods

        public void CheckAllPlayers()
        {
            if (!PhotonHelper.IsInRoom)
            {
                PlayerModResults.Clear();
                return;
            }

            if (!GorillaParent.hasInstance || GorillaParent.instance.vrrigs == null)
                return;

            foreach (VRRig rig in GorillaParent.instance.vrrigs)
            {
                if (rig == null || rig.OwningNetPlayer == null)
                    continue;

                var result = CheckPlayer(rig);
                PlayerModResults[rig] = result;

                string oderId = rig.OwningNetPlayer.UserId;
                if (result.HasIllegalMods && !CheckedPlayers.Contains(oderId))
                {
                    OnIllegalModDetected?.Invoke(result);
                    Debug.LogWarning($"[ModChecker] Illegal mods detected on {result.PlayerName}: {string.Join(", ", result.IllegalMods)}");
                    CheckedPlayers.Add(oderId);
                }
            }
        }

        public ModCheckResult CheckPlayer(VRRig rig)
        {
            var result = new ModCheckResult
            {
                Rig = rig,
                PlayerName = rig.OwningNetPlayer?.NickName ?? "Unknown"
            };

            if (rig?.OwningNetPlayer == null)
                return result;

            Player photonPlayer = rig.OwningNetPlayer.GetPlayerRef();
            result.Player = photonPlayer;

            if (photonPlayer?.CustomProperties == null)
                return result;

            Hashtable properties = photonPlayer.CustomProperties;

            foreach (object keyObj in properties.Keys)
            {
                string key = keyObj?.ToString();
                if (string.IsNullOrEmpty(key))
                    continue;

                if (key == "didTutorial")
                    continue;

                result.AllMods.Add(key);

                if (ModDatabaseFetcher.KnownCheats.TryGetValue(key, out string cheatName))
                {
                    result.IllegalMods.Add(cheatName);
                }
                else if (ModDatabaseFetcher.KnownMods.TryGetValue(key, out string modName))
                {
                    result.LegalMods.Add(modName);
                }
                else
                {
                    result.UnknownMods.Add(key);
                }
            }

            return result;
        }

        public ModCheckResult GetCachedResult(int actorNumber)
        {
            foreach (var kvp in PlayerModResults)
            {
                if (kvp.Key?.OwningNetPlayer?.ActorNumber == actorNumber)
                    return kvp.Value;
            }
            return null;
        }

        public ModCheckResult GetCachedResult(VRRig rig)
        {
            return PlayerModResults.TryGetValue(rig, out var result) ? result : null;
        }

        public void ClearCache()
        {
            PlayerModResults.Clear();
            CheckedPlayers.Clear();
        }

        public List<ModCheckResult> GetPlayersWithIllegalMods()
        {
            var results = new List<ModCheckResult>();

            foreach (var kvp in PlayerModResults)
            {
                if (kvp.Value.HasIllegalMods)
                    results.Add(kvp.Value);
            }

            return results;
        }

        public string GetRoomModSummary()
        {
            if (!PhotonHelper.IsInRoom)
                return "Not in room";

            int total = PlayerModResults.Count;
            int withMods = 0;
            int withIllegal = 0;

            foreach (var kvp in PlayerModResults)
            {
                if (kvp.Value.HasMods) withMods++;
                if (kvp.Value.HasIllegalMods) withIllegal++;
            }

            return $"Players: {total} | With Mods: {withMods} | Illegal: {withIllegal}";
        }

        public void OnPlayerLeft(VRRig rig)
        {
            if (rig == null) return;
            PlayerModResults.Remove(rig);
        }

        #endregion

        #region Private Methods

        private void SyncLobbyTracking()
        {
            string roomName = PhotonNetwork.CurrentRoom?.Name;
            if (roomName != currentRoom)
            {
                ResetLobbyTracking();
            }
        }

        private void ResetLobbyTracking()
        {
            CheckedPlayers.Clear();
            currentRoom = PhotonNetwork.CurrentRoom?.Name;
        }

        #endregion
    }
}
