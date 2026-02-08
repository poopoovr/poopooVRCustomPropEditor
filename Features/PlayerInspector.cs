using System;
using GorillaLocomotion;
using poopooVRCustomPropEditor.Utils;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Keyboard = UnityEngine.InputSystem.Keyboard;

namespace poopooVRCustomPropEditor.Features
{
    public class PlayerInspector : MonoBehaviour
    {
        #region Properties

        public VRRig SelectedRig { get; private set; }
        public bool IsActive { get; set; } = false;
        public event Action<VRRig> OnPlayerSelected;
        public event Action OnSelectionCleared;
        public VRRig PointedRig { get; private set; }

        #endregion

        #region Private Fields

        private LineRenderer pointerLine;
        private GameObject playerHighlighter;
        private Transform rightController;
        private Color mainColor = new Color(0.3f, 0.5f, 0.8f, 0.7f);
        private Vector3 lastSelectedPos;
        private float lastUpdateTime;

        #endregion

        #region Public Data

        public int SelectedFPS => SelectedRig?.fps ?? 0;
        public int SelectedPing => Plugin.Instance?.PlayerInfo?.GetPing(SelectedRig) ?? 0;
        public Color SelectedColor => SelectedRig?.playerColor ?? Color.white;
        public string SelectedName => SelectedRig?.OwningNetPlayer?.NickName ?? "Unknown";
        public PlayerPlatform SelectedPlatform => Plugin.Instance?.PlayerInfo?.GetPlatform(SelectedRig) ?? PlayerPlatform.Unknown;
        public Vector3 SelectedVelocity { get; private set; }

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            CreatePointerLine();
            CreatePlayerHighlighter();
        }

        private void LateUpdate()
        {
            UpdateControllerReference();

            if (!IsActive || rightController == null)
            {
                if (pointerLine != null) pointerLine.enabled = false;
                if (playerHighlighter != null) playerHighlighter.SetActive(false);
                return;
            }

            if (SelectedRig == null)
            {
                HandlePointingMode();
            }
            else
            {
                HandleInspectionMode();
            }
        }

        #endregion

        #region Initialization

        private void CreatePointerLine()
        {
            GameObject lineObj = new GameObject("PlayerPointerLine");
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

            pointerLine.material.color = mainColor;
            pointerLine.startColor = mainColor;
            pointerLine.endColor = mainColor;
            pointerLine.enabled = false;
        }

        private void CreatePlayerHighlighter()
        {
            playerHighlighter = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerHighlighter.name = "PlayerHighlighter";
            Destroy(playerHighlighter.GetComponent<Collider>());

            Renderer renderer = playerHighlighter.GetComponent<Renderer>();
            Material mat = MakeMaterialTransparent(renderer.material);
            mat.color = new Color(mainColor.r, mainColor.g, mainColor.b, 0.5f);
            renderer.material = mat;

            playerHighlighter.layer = LayerMask.NameToLayer("Ignore Raycast");
            playerHighlighter.transform.localScale = Vector3.one * 0.5f;
            playerHighlighter.SetActive(false);
        }

        private void UpdateControllerReference()
        {
            if (GTPlayer.Instance != null && GTPlayer.Instance.RightHand.controllerTransform != null)
            {
                if (rightController == null)
                {
                    rightController = new GameObject("RightControllerRef").transform;
                    rightController.SetParent(transform);
                }

                rightController.position = GTPlayer.Instance.RightHand.controllerTransform.TransformPoint(
                    GTPlayer.Instance.RightHand.handOffset);
                rightController.rotation = GTPlayer.Instance.RightHand.controllerTransform.rotation *
                    GTPlayer.Instance.RightHand.handRotOffset;
            }
        }

        #endregion

        #region Pointing Mode

        private void HandlePointingMode()
        {
            Vector3 origin = rightController.position;
            Vector3 direction = rightController.forward;

            if (PhysicsRaycastForPlayer(origin, direction, out RaycastHit hit, out VRRig rig))
            {
                pointerLine.enabled = true;
                pointerLine.SetPosition(0, origin);
                pointerLine.SetPosition(1, hit.point);

                if (rig != null)
                {
                    PointedRig = rig;
                    HighlightPlayer(rig);

                    if (ControllerInputPoller.instance != null)
                    {
                        bool triggerPressed = ControllerInputPoller.instance.rightControllerIndexFloat > 0.5f ||
                                             Mouse.current?.leftButton.wasPressedThisFrame == true;

                        if (triggerPressed)
                        {
                            SelectPlayer(rig);
                        }
                    }
                }
                else
                {
                    PointedRig = null;
                    HighlightPlayer(null);
                }
            }
            else
            {
                pointerLine.enabled = false;
                PointedRig = null;
                HighlightPlayer(null);
            }
        }

        #endregion

        #region Inspection Mode

        private void HandleInspectionMode()
        {
            pointerLine.enabled = false;
            HighlightPlayer(SelectedRig);

            if (Time.time - lastUpdateTime > 0.1f)
            {
                float dt = Time.time - lastUpdateTime;
                SelectedVelocity = (SelectedRig.transform.position - lastSelectedPos) / dt;
                lastSelectedPos = SelectedRig.transform.position;
                lastUpdateTime = Time.time;
            }

            if (ControllerInputPoller.instance != null)
            {
                if (ControllerInputPoller.instance.rightControllerSecondaryButton)
                {
                    ClearSelection();
                }
            }

            try
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                    ClearSelection();
            }
            catch { /* Keyboard not available in VR */ }
        }

        #endregion

        #region Selection

        public void SelectPlayer(VRRig rig)
        {
            if (rig == null) return;

            SelectedRig = rig;
            lastSelectedPos = rig.transform.position;
            lastUpdateTime = Time.time;
            SelectedVelocity = Vector3.zero;

            Debug.Log($"[PlayerInspector] Selected player: {rig.OwningNetPlayer?.NickName}");
            OnPlayerSelected?.Invoke(rig);
        }

        public void ClearSelection()
        {
            SelectedRig = null;
            PointedRig = null;
            HighlightPlayer(null);

            Debug.Log("[PlayerInspector] Selection cleared");
            OnSelectionCleared?.Invoke();
        }

        #endregion

        #region Highlighting

        private void HighlightPlayer(VRRig rig)
        {
            if (rig == null)
            {
                playerHighlighter.transform.SetParent(null);
                playerHighlighter.SetActive(false);
            }
            else
            {
                playerHighlighter.transform.SetParent(rig.transform, false);
                playerHighlighter.transform.localPosition = Vector3.zero;
                playerHighlighter.transform.localRotation = Quaternion.identity;
                playerHighlighter.SetActive(true);
            }
        }

        #endregion

        #region Raycast

        private bool PhysicsRaycastForPlayer(Vector3 origin, Vector3 direction, out RaycastHit hit, out VRRig rig)
        {
            rig = null;
            hit = default;

            RaycastHit[] hits = Physics.RaycastAll(origin, direction, 1000f);

            if (hits.Length == 0)
                return false;

            float minDistance = float.MaxValue;

            foreach (RaycastHit h in hits)
            {
                VRRig hitRig = h.collider.GetComponentInParent<VRRig>();
                bool isValidHit = false;

                if (hitRig != null && !hitRig.isLocal)
                {
                    isValidHit = true;
                }
                else if (hitRig == null)
                {
                    if (GTPlayer.Instance != null)
                    {
                        int layer = 1 << h.collider.gameObject.layer;
                        if ((layer & GTPlayer.Instance.locomotionEnabledLayers) != 0)
                        {
                            isValidHit = true;
                        }
                    }
                    else
                    {
                        isValidHit = true;
                    }
                }

                if (isValidHit && h.distance < minDistance)
                {
                    minDistance = h.distance;
                    hit = h;
                    rig = hitRig;
                }
            }

            return hit.collider != null;
        }

        #endregion

        #region Helpers

        private Material MakeMaterialTransparent(Material material)
        {
            Shader uberShader = Shader.Find("GorillaTag/UberShader");
            if (uberShader != null)
            {
                material.shader = uberShader;
            }

            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
            material.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_AlphaToMask", 0);
            material.renderQueue = (int)RenderQueue.Transparent;

            return material;
        }

        public string GetColorCode(Color color)
        {
            int r = Mathf.RoundToInt(color.r * 9);
            int g = Mathf.RoundToInt(color.g * 9);
            int b = Mathf.RoundToInt(color.b * 9);
            return $"<color=red>{r}</color> <color=green>{g}</color> <color=blue>{b}</color>";
        }

        public string DetectWorldScale(VRRig rig)
        {
            if (rig == null || rig.leftHand == null || rig.head == null)
                return "";

            Vector3 leftHandPos = rig.leftHand.rigTarget.position;
            Vector3 headPos = rig.head.rigTarget.position;

            Renderer renderer = (rig.headMesh != null ? rig.headMesh.GetComponent<Renderer>() : null) ?? rig.faceSkin;
            float headRadius = renderer != null ? renderer.bounds.size.y / 2f : 0.15f;

            Vector3 aboveHead = headPos + rig.head.rigTarget.up * headRadius;
            bool above = leftHandPos.y > aboveHead.y;

            float horizontalDist = Vector2.Distance(
                new Vector2(leftHandPos.x, leftHandPos.z),
                new Vector2(aboveHead.x, aboveHead.z));

            if (!above || horizontalDist > 0.35f)
                return "";

            float verticalDist = Vector3.Distance(leftHandPos, aboveHead);
            float wsPercent = Mathf.Round(0.23f / verticalDist * 100f / 5f) * 5f;
            wsPercent = Mathf.Clamp(wsPercent, 20f, 200f);

            if (wsPercent < 100f && wsPercent >= 20f)
                return $"WS:{wsPercent:F0}%";

            return "";
        }

        #endregion

        #region Public Methods

        public void SetColor(Color color)
        {
            mainColor = color;

            if (pointerLine != null)
            {
                pointerLine.material.color = new Color(color.r, color.g, color.b, 0.7f);
                pointerLine.startColor = new Color(color.r, color.g, color.b, 0.7f);
                pointerLine.endColor = new Color(color.r, color.g, color.b, 0.7f);
            }

            if (playerHighlighter != null)
            {
                Renderer renderer = playerHighlighter.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = new Color(color.r, color.g, color.b, 0.5f);
            }
        }

        #endregion
    }
}
