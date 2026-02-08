using System;
using System.Text;
using GorillaLocomotion;
using poopooVRCustomPropEditor.Features;
using poopooVRCustomPropEditor.Utils;
using UnityEngine;
using UnityEngine.InputSystem;
using Keyboard = UnityEngine.InputSystem.Keyboard;

namespace poopooVRCustomPropEditor.UI
{
    public enum MenuButtonType
    {
        LeftSecondary,
        RightSecondary,
        LeftPrimary,
        RightPrimary
    }

    public class MenuManager : MonoBehaviour
    {
        #region Properties

        public FloatingMenu Menu { get; private set; }
        public bool IsMenuVisible => Menu?.IsVisible ?? false;
        public MenuButtonType MenuOpenButton { get; set; } = MenuButtonType.LeftSecondary;
        public Transform RealLeftController { get; private set; }
        public Transform RealRightController { get; private set; }

        #endregion

        #region Private Fields

        private MediaController mediaController;
        private ModChecker modChecker;
        private PlayerInfo playerInfo;
        private PlayerInspector playerInspector;

        private bool wasButtonPressed = false;
        private float lastUpdateTime;
        private const float UI_UPDATE_INTERVAL = 0.5f;

        private LineRenderer pointerLine;
        private MenuButton lastHoveredButton;
        private bool initialized = false;
        private bool loggedFirstUpdate = false;
        private bool wasTouching = false;

        #endregion

        #region Initialization

        private void Start()
        {
            try
            {
                Plugin.Log.LogInfo("[MenuManager] Start() called");

                mediaController = GetComponent<MediaController>();
                modChecker = GetComponent<ModChecker>();
                playerInfo = GetComponent<PlayerInfo>();
                playerInspector = GetComponent<PlayerInspector>();

                Plugin.Log.LogInfo($"[MenuManager] Components: media={mediaController != null}, mod={modChecker != null}, info={playerInfo != null}, inspector={playerInspector != null}");

                CreateControllerTracking();
                CreateMenu();
                CreatePointerLine();

                initialized = true;
                Plugin.Log.LogInfo("[MenuManager] Fully initialized, ready for input");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[MenuManager] Start() FAILED: {ex}");
            }
        }

        private void CreateControllerTracking()
        {
            RealRightController = new GameObject("RealRightController").transform;
            RealLeftController = new GameObject("RealLeftController").transform;
            Plugin.Log.LogInfo("[MenuManager] Controller tracking objects created");
        }

        private void CreateMenu()
        {
            Plugin.Log.LogInfo("[MenuManager] Creating menu...");

            GameObject menuObj = new GameObject("FloatingMenuContainer");
            menuObj.transform.SetParent(RealLeftController);

            Menu = menuObj.AddComponent<FloatingMenu>();
            Menu.Initialize(RealLeftController);

            Menu.OnButtonClicked += HandleButtonClick;
            Menu.OnTabChanged += HandleTabChanged;

            Plugin.Log.LogInfo($"[MenuManager] Menu created, IsVisible={IsMenuVisible}");
        }

        private void CreatePointerLine()
        {
            GameObject lineObj = new GameObject("PointerLine");
            lineObj.transform.SetParent(transform);

            pointerLine = lineObj.AddComponent<LineRenderer>();
            pointerLine.startWidth = 0.0125f;
            pointerLine.endWidth = 0.0125f;
            pointerLine.positionCount = 2;

            Shader uberShader = Shader.Find("GorillaTag/UberShader");
            if (uberShader != null)
            {
                pointerLine.material = new Material(uberShader);
            }
            else
            {
                pointerLine.material = new Material(Shader.Find("Sprites/Default"));
            }

            pointerLine.startColor = new Color(0.3f, 0.5f, 0.8f, 0.7f);
            pointerLine.endColor = new Color(0.3f, 0.5f, 0.8f, 0.7f);
            pointerLine.enabled = false;
        }

        #endregion

        #region Update Loop

        private void Update()
        {
            if (!initialized) return;

            if (!loggedFirstUpdate)
            {
                Plugin.Log.LogInfo("[MenuManager] First Update() running");
                Plugin.Log.LogInfo($"[MenuManager] ControllerInputPoller.instance = {(ControllerInputPoller.instance != null ? "OK" : "NULL")}");
                Plugin.Log.LogInfo($"[MenuManager] GTPlayer.Instance = {(GTPlayer.Instance != null ? "OK" : "NULL")}");
                loggedFirstUpdate = true;
            }

            try
            {
                HandleMenuToggle();

                if (IsMenuVisible)
                {
                    HandleVRInteraction();
                    UpdateMenuContent();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[MenuManager] Update error: {ex}");
            }
        }

        private void LateUpdate()
        {
            if (!initialized) return;

            try
            {
                var player = GTPlayer.Instance;
                if (player == null) return;

                var leftCtrl = player.LeftHand.controllerTransform;
                if (leftCtrl != null)
                {
                    RealLeftController.position =
                        leftCtrl.TransformPoint(player.LeftHand.handOffset);
                    RealLeftController.rotation =
                        leftCtrl.rotation * player.LeftHand.handRotOffset;
                }

                var rightCtrl = player.RightHand.controllerTransform;
                if (rightCtrl != null)
                {
                    RealRightController.position =
                        rightCtrl.TransformPoint(player.RightHand.handOffset);
                    RealRightController.rotation =
                        rightCtrl.rotation * player.RightHand.handRotOffset;
                }
            }
            catch { /* controllers not ready yet, will retry next frame */ }
        }

        #endregion

        #region Input Handling

        public void HandleInput()
        {
            HandleMenuToggle();

            if (IsMenuVisible)
            {
                HandleVRInteraction();
                UpdateMenuContent();
            }
        }

        private void HandleMenuToggle()
        {
            try
            {
                if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
                {
                    Plugin.Log.LogInfo("[MenuManager] Tab key pressed, toggling menu");
                    Menu?.Toggle();
                    return;
                }
            }
            catch { /* Keyboard not available in VR, ignore */ }

            if (ControllerInputPoller.instance == null)
                return;

            bool isPressed = MenuOpenButton switch
            {
                MenuButtonType.LeftSecondary => ControllerInputPoller.instance.leftControllerSecondaryButton,
                MenuButtonType.RightSecondary => ControllerInputPoller.instance.rightControllerSecondaryButton,
                MenuButtonType.LeftPrimary => ControllerInputPoller.instance.leftControllerPrimaryButton,
                MenuButtonType.RightPrimary => ControllerInputPoller.instance.rightControllerPrimaryButton,
                _ => false
            };

            if (isPressed && !wasButtonPressed)
            {
                Plugin.Log.LogInfo($"[MenuManager] VR button pressed (type={MenuOpenButton}), toggling menu");
                Menu?.Toggle();
            }

            wasButtonPressed = isPressed;
        }

        private void HandleVRInteraction()
        {
            if (RealRightController == null || Menu == null) return;

            Vector3 fingerTip = RealRightController.position + RealRightController.forward * 0.1f;

            MenuButton closestButton = null;
            float closestDist = 0.08f;

            foreach (MenuButton btn in Menu.GetComponentsInChildren<MenuButton>(true))
            {
                if (btn == null || !btn.gameObject.activeInHierarchy) continue;
                float dist = Vector3.Distance(fingerTip, btn.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestButton = btn;
                }
            }

            if (closestButton == null)
            {
                Vector3 origin = RealRightController.position;
                Vector3 direction = RealRightController.forward;
                if (Physics.Raycast(origin, direction, out RaycastHit hit, 1.5f))
                {
                    closestButton = hit.collider?.GetComponent<MenuButton>();
                }
            }

            if (closestButton != lastHoveredButton)
            {
                lastHoveredButton?.OnPointerExit();
                closestButton?.OnPointerEnter();
                lastHoveredButton = closestButton;
            }

            if (closestButton != null)
            {
                bool fingerPress = closestDist < 0.03f;
                bool triggerPress = ControllerInputPoller.instance != null &&
                                   ControllerInputPoller.instance.rightControllerIndexFloat > 0.5f;

                if ((fingerPress || triggerPress) && !wasTouching)
                {
                    closestButton.OnPointerDown();
                    closestButton.OnPointerUp();
                    wasTouching = true;
                }
                else if (!fingerPress && !triggerPress)
                {
                    wasTouching = false;
                }
            }
            else
            {
                wasTouching = false;
            }

            if (pointerLine != null)
                pointerLine.enabled = false;
        }

        #endregion

        #region Menu Content Updates

        private void UpdateMenuContent()
        {
            if (Time.time - lastUpdateTime < UI_UPDATE_INTERVAL)
                return;

            lastUpdateTime = Time.time;

            switch (Menu.CurrentTab)
            {
                case MenuTab.Media:
                    UpdateMediaTab();
                    break;
                case MenuTab.Players:
                    UpdatePlayersTab();
                    break;
                case MenuTab.Info:
                    UpdateInfoTab();
                    break;
            }
        }

        private void UpdateMediaTab()
        {
            if (mediaController == null) return;

            var media = mediaController.CurrentMedia;

            if (media != null && media.HasMedia)
            {
                Menu.UpdateMediaInfo(
                    media.Title,
                    media.Artist,
                    media.FormattedTime,
                    media.IsPlaying
                );
            }
            else if (mediaController.IsQuickSongAvailable)
            {
                Menu.UpdateMediaInfo("No media playing", "", "--:-- / --:--", false);
            }
            else
            {
                Menu.UpdateMediaInfo("QuickSong not found", "Place QuickSong.exe in game folder", "", false);
            }
        }

        private void UpdatePlayersTab()
        {
            if (playerInfo == null || modChecker == null) return;

            if (!PhotonHelper.IsInRoom)
            {
                Menu.UpdatePlayerList("Not in a room\n\nJoin a room to see players.");
                return;
            }

            StringBuilder sb = new StringBuilder();

            if (playerInspector != null && playerInspector.SelectedRig != null)
            {
                VRRig rig = playerInspector.SelectedRig;
                var modResult = modChecker.GetCachedResult(rig);

                sb.AppendLine($"<b>=== {playerInspector.SelectedName} ===</b>");
                sb.AppendLine();

                string platformDisplay = playerInspector.SelectedPlatform switch
                {
                    Features.PlayerPlatform.Steam => "<color=#0091F7>Steam</color>",
                    Features.PlayerPlatform.OculusPC => "<color=#0091F7>Oculus PCVR</color>",
                    Features.PlayerPlatform.PC => "PC",
                    Features.PlayerPlatform.Standalone => "<color=#26A6FF>Standalone</color>",
                    _ => "Unknown"
                };
                sb.AppendLine($"Platform: {platformDisplay}");

                int fps = playerInspector.SelectedFPS;
                string fpsColor = fps < 60 ? "red" : fps < 72 ? "yellow" : "green";
                sb.AppendLine($"FPS: <color={fpsColor}>{fps}</color>");

                int ping = playerInspector.SelectedPing;
                string pingColor = ping > 250 ? "red" : ping > 100 ? "orange" : "green";
                sb.AppendLine($"Ping: <color={pingColor}>{ping}</color> ms");

                sb.AppendLine($"Color: {playerInspector.GetColorCode(playerInspector.SelectedColor)}");

                float speed = playerInspector.SelectedVelocity.magnitude;
                string speedColor = speed < 6.5f ? "green" : speed < 10f ? "orange" : "red";
                sb.AppendLine($"Speed: <color={speedColor}>{speed:F1}</color> m/s");

                string ws = playerInspector.DetectWorldScale(rig);
                if (!string.IsNullOrEmpty(ws))
                {
                    sb.AppendLine($"<color=yellow>{ws}</color>");
                }

                if (modResult != null && modResult.HasMods)
                {
                    sb.AppendLine();
                    sb.AppendLine($"Mods: {modResult.GetFormattedModsList()}");
                }

                sb.AppendLine();
                sb.AppendLine("<i>Press B to deselect</i>");
            }
            else
            {
                sb.AppendLine("<i>Point at a player and pull trigger to inspect</i>");
                sb.AppendLine();

                var players = playerInfo.Players;

                foreach (var player in players)
                {
                    if (player?.Rig == null) continue;

                    var modResult = modChecker.GetCachedResult(player.Rig);

                    string colorTag = "white";

                    if (modResult != null && modResult.HasMods)
                    {
                        if (modResult.HasIllegalMods)
                            colorTag = "red";
                        else if (modResult.LegalMods.Count > 0)
                            colorTag = "green";
                        else if (modResult.UnknownMods.Count > 0)
                            colorTag = "yellow";
                    }

                    string localTag = player.IsLocal ? " (You)" : "";
                    string masterTag = player.IsMasterClient ? " [Host]" : "";

                    bool isPointed = (playerInspector?.PointedRig == player.Rig);
                    string pointer = isPointed ? "► " : "  ";

                    string fpsColor = player.FPS < 60 ? "red" : player.FPS < 72 ? "yellow" : "green";
                    string pingColor = player.Ping > 250 ? "red" : player.Ping > 100 ? "orange" : "green";

                    sb.AppendLine($"{pointer}<color={colorTag}>{player.NickName}{localTag}{masterTag}</color>");
                    sb.AppendLine($"    {player.PlatformDisplayName} | <color={fpsColor}>{player.FPS}</color> FPS | <color={pingColor}>{player.Ping}</color>ms");

                    if (modResult != null && modResult.HasMods)
                    {
                        sb.AppendLine($"    {modResult.GetFormattedModsList()}");
                    }
                }
            }

            Menu.UpdatePlayerList(sb.ToString());
        }

        private void UpdateInfoTab()
        {
            if (playerInfo == null) return;

            Menu.UpdateRoomInfo(
                playerInfo.RoomCode,
                PhotonHelper.GetPlayerCount()
            );

            if (playerInfo.LocalPlayerData != null)
            {
                var local = playerInfo.LocalPlayerData;
                Menu.UpdateLocalPlayerInfo(
                    local.PlatformDisplayName,
                    (int)local.Ping,
                    $"FPS: {local.FPS}"
                );
            }
        }

        #endregion

        #region Button Handlers

        private void HandleButtonClick(string buttonId)
        {
            Plugin.Log.LogInfo($"[MenuManager] Button clicked: {buttonId}");

            switch (buttonId)
            {
                case "media_prev":
                    mediaController?.PreviousTrack();
                    break;
                case "media_playpause":
                    mediaController?.PlayPause();
                    break;
                case "media_next":
                    mediaController?.NextTrack();
                    break;
                case "media_volup":
                    mediaController?.VolumeUp();
                    break;
                case "media_voldown":
                    mediaController?.VolumeDown();
                    break;
                case "players_refresh":
                    playerInfo?.RefreshPlayerData();
                    modChecker?.CheckAllPlayers();
                    UpdatePlayersTab();
                    break;
            }
        }

        private void HandleTabChanged(MenuTab newTab)
        {
            Plugin.Log.LogInfo($"[MenuManager] Tab changed to: {newTab}");

            if (playerInspector != null)
            {
                playerInspector.IsActive = (newTab == MenuTab.Players);

                if (newTab != MenuTab.Players)
                {
                    playerInspector.ClearSelection();
                }
            }

            lastUpdateTime = 0;
            UpdateMenuContent();
        }

        #endregion

        #region Public Methods

        public void ShowMenu()
        {
            Menu?.Show();
        }

        public void HideMenu()
        {
            Menu?.Hide();
            if (pointerLine != null)
                pointerLine.enabled = false;
        }

        public void ToggleMenu()
        {
            if (IsMenuVisible)
                HideMenu();
            else
                ShowMenu();
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            if (Menu != null)
            {
                Menu.OnButtonClicked -= HandleButtonClick;
                Menu.OnTabChanged -= HandleTabChanged;
            }
        }

        #endregion
    }
}
