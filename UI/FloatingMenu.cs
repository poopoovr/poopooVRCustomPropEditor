using System;
using System.Collections;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace poopooVRCustomPropEditor.UI
{
    public enum MenuTab
    {
        Media,
        Players,
        Info
    }

    public class FloatingMenu : MonoBehaviour
    {
        #region Properties

        public bool IsVisible { get; private set; }
        public MenuTab CurrentTab { get; private set; } = MenuTab.Media;
        public bool IsLoaded { get; private set; } = false;

        public event Action<MenuTab> OnTabChanged;
        public event Action<string> OnButtonClicked;

        #endregion

        #region Private Fields

        private GameObject menuRoot;
        private Transform trackingHand;

        private readonly Vector3 menuLocalPosition = new Vector3(0f, -0.26f, 0.04f);
        private readonly Quaternion menuLocalRotation = Quaternion.Euler(-60f, 0f, 180f);
        private Vector3 targetScale;

        private GameObject mediaTab;
        private GameObject playerTab;
        private GameObject infoTab;

        private TextMeshPro titleText;
        private TextMeshPro songTitleText;
        private TextMeshPro songArtistText;
        private TextMeshPro playerListText;
        private TextMeshPro roomInfoText;
        private TextMeshPro localPlayerInfoText;

        private MenuButton mediaTabBtn;
        private MenuButton playerTabBtn;
        private MenuButton infoTabBtn;

        private readonly Color activeTabColor = new Color(0.3f, 0.5f, 0.8f, 1f);
        private readonly Color inactiveTabColor = new Color(0.384f, 0f, 0.553f, 1f);

        private Coroutine currentAnimation;
        private AssetBundle loadedBundle;

        #endregion

        #region Initialization

        public void Initialize(Transform handTransform = null)
        {
            trackingHand = handTransform;
            Plugin.Log.LogInfo($"[FloatingMenu] Initialize called, hand={handTransform?.name ?? "null"}");
            StartCoroutine(LoadAndSetup());
        }

        private IEnumerator LoadAndSetup()
        {
            AssetBundle bundle = null;

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = null;

                foreach (string name in assembly.GetManifestResourceNames())
                {
                    Plugin.Log.LogInfo($"[FloatingMenu] Embedded resource: {name}");
                    if (name.ToLower().Contains("menu"))
                    {
                        resourceName = name;
                    }
                }

                if (resourceName != null)
                {
                    using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            byte[] data = new byte[stream.Length];
                            stream.Read(data, 0, data.Length);
                            bundle = AssetBundle.LoadFromMemory(data);
                            loadedBundle = bundle;
                            Plugin.Log.LogInfo($"[FloatingMenu] AssetBundle loaded from embedded resource ({data.Length} bytes)");
                        }
                    }
                }
                else
                {
                    Plugin.Log.LogWarning("[FloatingMenu] No embedded menu resource found, trying file...");
                    string dllPath = Path.GetDirectoryName(assembly.Location);
                    string bundlePath = Path.Combine(dllPath, "menu");

                    if (File.Exists(bundlePath))
                    {
                        bundle = AssetBundle.LoadFromFile(bundlePath);
                        loadedBundle = bundle;
                        Plugin.Log.LogInfo($"[FloatingMenu] AssetBundle loaded from file: {bundlePath}");
                    }
                    else
                    {
                        Plugin.Log.LogError($"[FloatingMenu] Bundle not found at: {bundlePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[FloatingMenu] Failed to load AssetBundle: {ex}");
            }

            yield return null;

            if (bundle == null)
            {
                Plugin.Log.LogError("[FloatingMenu] AssetBundle is null! Cannot create menu.");
                yield break;
            }

            string[] allNames = bundle.GetAllAssetNames();
            foreach (string n in allNames)
            {
                Plugin.Log.LogInfo($"[FloatingMenu] Bundle asset: {n}");
            }

            GameObject prefab = bundle.LoadAsset<GameObject>("Menu");
            if (prefab == null && allNames.Length > 0)
            {
                foreach (string n in allNames)
                {
                    prefab = bundle.LoadAsset<GameObject>(n);
                    if (prefab != null)
                    {
                        Plugin.Log.LogInfo($"[FloatingMenu] Loaded prefab from: {n}");
                        break;
                    }
                }
            }

            if (prefab == null)
            {
                Plugin.Log.LogError("[FloatingMenu] Failed to load Menu prefab from bundle!");
                yield break;
            }

            Plugin.Log.LogInfo($"[FloatingMenu] Prefab loaded: {prefab.name}");

            menuRoot = Instantiate(prefab);
            menuRoot.name = "MenuRoot";

            targetScale = new Vector3(0.5f, 0.5f, 0.5f);
            Plugin.Log.LogInfo($"[FloatingMenu] Target scale: {targetScale}");

            menuRoot.transform.SetParent(trackingHand, false);
            menuRoot.transform.localPosition = menuLocalPosition;
            menuRoot.transform.localRotation = menuLocalRotation;
            menuRoot.transform.localScale = Vector3.zero;

            LogHierarchy(menuRoot.transform, "");

            FixAllShaders(menuRoot);

            FindReferences();

            SetupInteractivity();

            menuRoot.SetActive(false);
            IsVisible = false;
            IsLoaded = true;

            SwitchTab(MenuTab.Media);

            Plugin.Log.LogInfo("[FloatingMenu] AssetBundle menu fully initialized!");
        }

        private void LogHierarchy(Transform t, string indent)
        {
            string components = "";
            if (t.GetComponent<TextMeshPro>() != null) components += "[TMP]";
            if (t.GetComponent<MeshRenderer>() != null) components += "[MeshRenderer]";
            if (t.GetComponent<MeshFilter>() != null) components += "[MeshFilter]";
            if (t.GetComponent<Collider>() != null) components += "[Collider]";

            Plugin.Log.LogInfo($"[FloatingMenu] {indent}{t.name} {components} pos={t.localPosition} scale={t.localScale}");

            for (int i = 0; i < t.childCount; i++)
            {
                LogHierarchy(t.GetChild(i), indent + "  ");
            }
        }

        #endregion

        #region Shader Fixing

        private void FixAllShaders(GameObject root)
        {
            Shader uberShader = Shader.Find("GorillaTag/UberShader");
            if (uberShader != null)
            {
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.GetComponent<TextMeshPro>() != null) continue;

                    foreach (Material mat in r.materials)
                    {
                        Color origColor = mat.HasProperty("_Color") ? mat.color : Color.white;
                        Texture origTex = mat.HasProperty("_MainTex") ? mat.mainTexture : null;
                        mat.shader = uberShader;
                        mat.color = origColor;
                        if (origTex != null) mat.mainTexture = origTex;
                    }
                }
                Plugin.Log.LogInfo("[FloatingMenu] Swapped renderers to UberShader");
            }
            else
            {
                Plugin.Log.LogWarning("[FloatingMenu] UberShader not found!");
            }

            Shader tmpShader = Shader.Find("TextMeshPro/Mobile/Distance Field");
            if (tmpShader != null)
            {
                foreach (TextMeshPro tmp in root.GetComponentsInChildren<TextMeshPro>(true))
                {
                    if (tmp.fontMaterial != null)
                    {
                        tmp.fontMaterial = new Material(tmp.fontMaterial) { shader = tmpShader };
                    }
                }
                Plugin.Log.LogInfo("[FloatingMenu] Fixed TMP shaders");
            }
            else
            {
                Plugin.Log.LogWarning("[FloatingMenu] TMP shader not found!");
            }
        }

        #endregion

        #region Reference Finding

        private void FindReferences()
        {
            mediaTab = FindChild(menuRoot, "MediaTab");
            playerTab = FindChild(menuRoot, "PlayerTab");
            infoTab = FindChild(menuRoot, "InfoTab");

            titleText = FindTMP(menuRoot, "Title") ?? FindTMP(menuRoot, "Text (TMP)");
            songTitleText = FindTMP(menuRoot, "SongTitle");
            songArtistText = FindTMP(menuRoot, "SongArtist");
            playerListText = FindTMP(menuRoot, "PlayerListText");
            roomInfoText = FindTMP(menuRoot, "RoomInfoText") ?? FindTMP(menuRoot, "roominfotext");
            localPlayerInfoText = FindTMP(menuRoot, "LocalPlayerInfoText") ?? FindTMP(menuRoot, "localplayerinfotext");

            Plugin.Log.LogInfo($"[FloatingMenu] References: " +
                $"mediaTab={mediaTab != null}, playerTab={playerTab != null}, infoTab={infoTab != null}, " +
                $"title={titleText != null}, songTitle={songTitleText != null}, songArtist={songArtistText != null}, " +
                $"playerList={playerListText != null}, roomInfo={roomInfoText != null}, localInfo={localPlayerInfoText != null}");
        }

        #endregion

        #region Interactivity Setup

        private void SetupInteractivity()
        {
            mediaTabBtn = SetupButton("MediaTabButton", () =>
            {
                SwitchTab(MenuTab.Media);
                OnTabChanged?.Invoke(MenuTab.Media);
            });
            playerTabBtn = SetupButton("PlayerTabButton", () =>
            {
                SwitchTab(MenuTab.Players);
                OnTabChanged?.Invoke(MenuTab.Players);
            });
            infoTabBtn = SetupButton("InfoTabButton", () =>
            {
                SwitchTab(MenuTab.Info);
                OnTabChanged?.Invoke(MenuTab.Info);
            });

            SetupButton("PrevButton", () => OnButtonClicked?.Invoke("media_prev"));
            SetupButton("PausePlayButton", () => OnButtonClicked?.Invoke("media_playpause"));
            SetupButton("PlayPauseButton", () => OnButtonClicked?.Invoke("media_playpause"));
            SetupButton("NextButton", () => OnButtonClicked?.Invoke("media_next"));
        }

        private MenuButton SetupButton(string name, Action onClick)
        {
            GameObject obj = FindChild(menuRoot, name);
            if (obj == null)
            {
                Plugin.Log.LogWarning($"[FloatingMenu] Button '{name}' not found in prefab hierarchy");
                return null;
            }

            BoxCollider col = obj.GetComponent<BoxCollider>();
            if (col == null)
            {
                Collider existingCol = obj.GetComponent<Collider>();
                if (existingCol != null) Destroy(existingCol);

                col = obj.AddComponent<BoxCollider>();
            }
            col.size = new Vector3(1.2f, 1.2f, 2f);
            col.isTrigger = true;

            MenuButton btn = obj.AddComponent<MenuButton>();
            btn.Initialize(name);
            btn.OnClick += onClick;

            Plugin.Log.LogInfo($"[FloatingMenu] Button '{name}' ready");
            return btn;
        }

        #endregion

        #region Helper Methods

        private GameObject FindChild(GameObject parent, string name)
        {
            Transform t = FindChildTransform(parent.transform, name);
            return t?.gameObject;
        }

        private Transform FindChildTransform(Transform parent, string name)
        {
            Transform found = parent.Find(name);
            if (found != null) return found;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
                    return child;

                found = FindChildTransform(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private TextMeshPro FindTMP(GameObject root, string name)
        {
            GameObject obj = FindChild(root, name);
            return obj?.GetComponent<TextMeshPro>();
        }

        #endregion

        #region Tab Management

        public void SwitchTab(MenuTab tab)
        {
            CurrentTab = tab;

            bool canSwitch = (tab == MenuTab.Media && mediaTab != null)
                          || (tab == MenuTab.Players && playerTab != null)
                          || (tab == MenuTab.Info && infoTab != null);

            if (canSwitch)
            {
                if (mediaTab != null) mediaTab.SetActive(tab == MenuTab.Media);
                if (playerTab != null) playerTab.SetActive(tab == MenuTab.Players);
                if (infoTab != null) infoTab.SetActive(tab == MenuTab.Info);
            }
            else
            {
                Plugin.Log.LogWarning($"[FloatingMenu] Tab panel for {tab} is null, keeping current view");
            }

            mediaTabBtn?.SetBaseColor(tab == MenuTab.Media ? activeTabColor : inactiveTabColor);
            playerTabBtn?.SetBaseColor(tab == MenuTab.Players ? activeTabColor : inactiveTabColor);
            infoTabBtn?.SetBaseColor(tab == MenuTab.Info ? activeTabColor : inactiveTabColor);
        }

        #endregion

        #region Content Updates

        public void UpdateMediaInfo(string title, string artist, string time, bool isPlaying)
        {
            if (songTitleText != null) songTitleText.text = title ?? "";
            if (songArtistText != null) songArtistText.text = artist ?? "";
        }

        public void UpdatePlayerList(string playerListContent)
        {
            if (playerListText != null) playerListText.text = playerListContent ?? "";
        }

        public void UpdateRoomInfo(string roomCode, int playerCount)
        {
            if (roomInfoText != null)
                roomInfoText.text = $"Room: <b>{roomCode ?? "---"}</b>\nPlayers: {playerCount}";
        }

        public void UpdateLocalPlayerInfo(string platform, int ping, string additionalInfo = "")
        {
            if (localPlayerInfoText != null)
            {
                string info = $"Platform: {platform}\nPing: {ping}ms";
                if (!string.IsNullOrEmpty(additionalInfo))
                    info += $"\n{additionalInfo}";
                localPlayerInfoText.text = info;
            }
        }

        #endregion

        #region Show/Hide

        public void Toggle()
        {
            Plugin.Log.LogInfo($"[FloatingMenu] Toggle called, visible={IsVisible}, loaded={IsLoaded}");
            if (!IsLoaded) return;
            if (IsVisible) Hide(); else Show();
        }

        public void Show()
        {
            if (menuRoot == null)
            {
                Plugin.Log.LogError("[FloatingMenu] menuRoot is null!");
                return;
            }

            if (currentAnimation != null)
                StopCoroutine(currentAnimation);

            currentAnimation = StartCoroutine(AnimateOpen());
        }

        public void Hide()
        {
            if (menuRoot == null) return;

            if (currentAnimation != null)
                StopCoroutine(currentAnimation);

            currentAnimation = StartCoroutine(AnimateClose());
        }

        private IEnumerator AnimateOpen()
        {
            menuRoot.SetActive(true);
            menuRoot.transform.localScale = Vector3.zero;

            float startTime = Time.time;
            float duration = 0.15f;

            while (Time.time - startTime < duration)
            {
                float t = (Time.time - startTime) / duration;
                menuRoot.transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, t);
                yield return null;
            }

            menuRoot.transform.localScale = targetScale;
            IsVisible = true;
            Plugin.Log.LogInfo($"[FloatingMenu] Menu opened, worldPos={menuRoot.transform.position}");
        }

        private IEnumerator AnimateClose()
        {
            menuRoot.transform.localScale = targetScale;
            float startTime = Time.time;
            float duration = 0.1f;

            while (Time.time - startTime < duration)
            {
                float t = (Time.time - startTime) / duration;
                menuRoot.transform.localScale = Vector3.Lerp(targetScale, Vector3.zero, t);
                yield return null;
            }

            menuRoot.transform.localScale = Vector3.zero;
            menuRoot.SetActive(false);
            IsVisible = false;
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            if (loadedBundle != null)
            {
                loadedBundle.Unload(true);
                loadedBundle = null;
            }
        }

        #endregion
    }
}
