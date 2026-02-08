using System;
using System.Collections.Generic;
using poopooVRCustomPropEditor.Utils;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace poopooVRCustomPropEditor.Features
{
    public enum PlayerPlatform
    {
        Unknown,
        Steam,
        OculusPC,
        PC,
        Standalone
    }

    public class PlayerData
    {
        public VRRig Rig { get; set; }
        public Player PhotonPlayer { get; set; }
        public string NickName { get; set; }
        public string UserId { get; set; }
        public PlayerPlatform Platform { get; set; }
        public int FPS { get; set; }
        public bool IsLocal { get; set; }
        public bool IsMasterClient { get; set; }
        public int ActorNumber { get; set; }
        public List<string> Mods { get; set; } = new List<string>();
        public int Ping { get; set; }
        public DateTime AccountCreated { get; set; }
        public Color PlayerColor { get; set; }

        public string PlatformDisplayName => Platform switch
        {
            PlayerPlatform.Steam => "<color=#0091F7>Steam</color>",
            PlayerPlatform.OculusPC => "<color=#0091F7>Oculus PCVR</color>",
            PlayerPlatform.PC => "PC",
            PlayerPlatform.Standalone => "<color=#26A6FF>Standalone</color>",
            _ => "Unknown"
        };

        public string PlatformDisplayNameClean => Platform switch
        {
            PlayerPlatform.Steam => "Steam",
            PlayerPlatform.OculusPC => "Oculus PCVR",
            PlayerPlatform.PC => "PC",
            PlayerPlatform.Standalone => "Standalone",
            _ => "Unknown"
        };
    }

    public class PlayerInfo : MonoBehaviour
    {
        #region Constants

        private static readonly DateTime OculusPayDay = new DateTime(2023, 02, 06);

        #endregion

        #region Properties

        public Dictionary<VRRig, PlayerData> PlayerDataMap { get; private set; } = new Dictionary<VRRig, PlayerData>();
        public Dictionary<VRRig, PlayerPlatform> PlayerPlatforms { get; private set; } = new Dictionary<VRRig, PlayerPlatform>();
        public Dictionary<VRRig, int> PlayerPing { get; private set; } = new Dictionary<VRRig, int>();
        public List<PlayerData> Players { get; private set; } = new List<PlayerData>();
        public PlayerData LocalPlayerData { get; private set; }
        public string RoomCode => PhotonHelper.CurrentRoomCode;
        public bool IsInRoom => PhotonHelper.IsInRoom;
        public float UpdateInterval { get; set; } = 2f;

        #endregion

        #region Private Fields

        private float lastUpdateTime;

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            if (Time.time - lastUpdateTime >= UpdateInterval)
            {
                RefreshPlayerData();
                lastUpdateTime = Time.time;
            }
        }

        #endregion

        #region Public Methods

        public void RefreshPlayerData()
        {
            Players.Clear();

            if (!PhotonHelper.IsInRoom)
            {
                LocalPlayerData = null;
                return;
            }

            if (GorillaParent.hasInstance && GorillaParent.instance.vrrigs != null)
            {
                foreach (VRRig rig in GorillaParent.instance.vrrigs)
                {
                    if (rig == null || rig.OwningNetPlayer == null)
                        continue;

                    var playerData = ExtractPlayerData(rig);
                    Players.Add(playerData);
                    PlayerDataMap[rig] = playerData;

                    if (rig.isLocal)
                    {
                        LocalPlayerData = playerData;
                    }
                }
            }
        }

        public PlayerData GetPlayerData(VRRig rig)
        {
            if (PlayerDataMap.TryGetValue(rig, out var data))
                return data;
            return ExtractPlayerData(rig);
        }

        public PlayerData GetPlayerByActorNumber(int actorNumber)
        {
            return Players.Find(p => p.ActorNumber == actorNumber);
        }

        public void OnPlayerCosmeticsLoaded(VRRig rig)
        {
            if (rig == null) return;

            var platform = DetectPlatformFromRig(rig);
            PlayerPlatforms[rig] = platform;

            Debug.Log($"[PlayerInfo] Detected platform for {rig.OwningNetPlayer?.NickName}: {platform}");
        }

        public void OnPlayerLeft(VRRig rig)
        {
            if (rig == null) return;

            PlayerPlatforms.Remove(rig);
            PlayerPing.Remove(rig);
            PlayerDataMap.Remove(rig);
        }

        public PlayerPlatform GetPlatform(VRRig rig)
        {
            return PlayerPlatforms.GetValueOrDefault(rig, PlayerPlatform.Unknown);
        }

        public int GetPing(VRRig rig)
        {
            return PlayerPing.TryGetValue(rig, out int ping) ? ping : PhotonNetwork.GetPing();
        }

        #endregion

        #region Private Methods

        private PlayerData ExtractPlayerData(VRRig rig)
        {
            if (rig == null) return null;

            var netPlayer = rig.OwningNetPlayer;
            var photonPlayer = netPlayer?.GetPlayerRef();

            var data = new PlayerData
            {
                Rig = rig,
                PhotonPlayer = photonPlayer,
                NickName = netPlayer?.NickName ?? "Unknown",
                UserId = netPlayer?.UserId ?? "N/A",
                ActorNumber = netPlayer?.ActorNumber ?? -1,
                IsLocal = rig.isLocal,
                IsMasterClient = photonPlayer?.IsMasterClient ?? false,
                Platform = GetPlatform(rig),
                FPS = rig.fps,
                Mods = new List<string>(),
                Ping = GetPing(rig),
                PlayerColor = rig.playerColor
            };

            if (data.Platform == PlayerPlatform.Unknown && rig.OwningNetPlayer != null)
            {
                data.Platform = DetectPlatformFromRig(rig);
                PlayerPlatforms[rig] = data.Platform;
            }

            return data;
        }

        private PlayerPlatform DetectPlatformFromRig(VRRig rig)
        {
            if (rig == null) return PlayerPlatform.Unknown;

            string cosmeticsAllowed = rig.rawCosmeticString?.ToLowerInvariant() ?? "";

            if (cosmeticsAllowed.Contains("s. first login"))
            {
                return PlayerPlatform.Steam;
            }

            if (cosmeticsAllowed.Contains("first login") || cosmeticsAllowed.Contains("game-purchase"))
            {
                return PlayerPlatform.OculusPC;
            }

            var photonPlayer = rig.OwningNetPlayer?.GetPlayerRef();
            if (photonPlayer?.CustomProperties?.Count > 1)
            {
                return PlayerPlatform.PC;
            }

            return PlayerPlatform.Standalone;
        }

        #endregion
    }
}
