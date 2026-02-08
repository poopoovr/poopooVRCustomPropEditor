using System;
using UnityEngine;

namespace poopooVRCustomPropEditor.UI
{
    public class MenuButton : MonoBehaviour
    {
        #region Properties

        public string ButtonId { get; private set; }
        public bool IsHovered { get; private set; }
        public bool IsPressed { get; private set; }
        public bool Interactable { get; set; } = true;

        public event Action OnClick;

        #endregion

        #region Private Fields

        private Renderer buttonRenderer;
        private Color normalColor;
        private Color highlightColor;
        private Color pressColor;

        #endregion

        #region Initialization

        public void Initialize(string id)
        {
            ButtonId = id;
            buttonRenderer = GetComponent<Renderer>();

            if (buttonRenderer != null && buttonRenderer.material != null)
            {
                normalColor = buttonRenderer.material.HasProperty("_Color")
                    ? buttonRenderer.material.color
                    : new Color(0.384f, 0f, 0.553f, 1f);

                highlightColor = BrightenColor(normalColor, 0.15f);
                pressColor = BrightenColor(normalColor, 0.3f);
            }
        }

        public void SetBaseColor(Color c)
        {
            normalColor = c;
            highlightColor = BrightenColor(c, 0.15f);
            pressColor = BrightenColor(c, 0.3f);
            UpdateVisual();
        }

        #endregion

        #region Interaction Methods

        public void OnPointerEnter()
        {
            if (!Interactable) return;
            IsHovered = true;
            UpdateVisual();
        }

        public void OnPointerExit()
        {
            IsHovered = false;
            IsPressed = false;
            UpdateVisual();
        }

        public void OnPointerDown()
        {
            if (!Interactable) return;
            IsPressed = true;
            UpdateVisual();
        }

        public void OnPointerUp()
        {
            if (!Interactable) return;

            if (IsPressed && IsHovered)
            {
                OnClick?.Invoke();
            }

            IsPressed = false;
            UpdateVisual();
        }

        #endregion

        #region Visual

        private void UpdateVisual()
        {
            if (buttonRenderer == null || buttonRenderer.material == null) return;

            if (IsPressed)
                buttonRenderer.material.color = pressColor;
            else if (IsHovered)
                buttonRenderer.material.color = highlightColor;
            else
                buttonRenderer.material.color = normalColor;
        }

        private static Color BrightenColor(Color c, float amount)
        {
            return new Color(
                Mathf.Min(c.r + amount, 1f),
                Mathf.Min(c.g + amount, 1f),
                Mathf.Min(c.b + amount, 1f),
                c.a
            );
        }

        #endregion
    }
}
