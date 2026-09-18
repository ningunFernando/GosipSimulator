using System;
using UnityEngine;
using UnityEngine.UIElements;
using GosipSimulator.Core;

namespace GosipSimulator.Debug
{
    /// <summary>
    /// Debug overlay on UI Toolkit, driven by bus events only: no polling and no Find (C5, R10).
    /// The Label is built here instead of in UXML so a build without this assembly shows nothing:
    /// the UIDocument stays in the scene, but nothing adds content to it.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class DebugHud : MonoBehaviour
    {
        public const string LABEL_NAME = "debug-hud-label";

        private readonly DebugHudModel _model = new DebugHudModel();
        private Label                  _label;

        // ────────────────────────────────
        // LIFECYCLE
        // ────────────────────────────────
        #region Lifecycle

        private void OnEnable()
        {
            // UIDocument rebuilds its root in its own OnEnable, which silently drops anything added
            // before. It runs first because UIDocument declares DefaultExecutionOrder(-100),
            // verified on 6000.6.0f1; the PlayMode HUD test fails if that ever changes.
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;

            if (root == null)
            {
                throw new InvalidOperationException(
                    "[DebugHud] UIDocument has no root visual element. Check its PanelSettings and execution order.");
            }

            _label = CreateLabel();
            root.Add(_label);
            Refresh();

            EventBus.Subscribe<OnBootstrapComplete>(HandleBootstrapComplete);
            EventBus.Subscribe<OnGameStateChanged>(HandleGameStateChanged);
            EventBus.Subscribe<OnProgressChanged>(HandleProgressChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<OnBootstrapComplete>(HandleBootstrapComplete);
            EventBus.Unsubscribe<OnGameStateChanged>(HandleGameStateChanged);
            EventBus.Unsubscribe<OnProgressChanged>(HandleProgressChanged);

            _label?.RemoveFromHierarchy();
            _label = null;
        }

        #endregion

        // ────────────────────────────────
        // EVENT HANDLERS
        // ────────────────────────────────
        #region Event Handlers

        private void HandleBootstrapComplete(OnBootstrapComplete e)
        {
            _model.MarkBootstrapComplete();
            Refresh();
        }

        private void HandleGameStateChanged(OnGameStateChanged e)
        {
            _model.SetState(e.newState);
            Refresh();
        }

        private void HandleProgressChanged(OnProgressChanged e)
        {
            _model.SetCurrency(e.currency);
            Refresh();
        }

        #endregion

        // ────────────────────────────────
        // PRIVATE
        // ────────────────────────────────
        #region Private

        private static Label CreateLabel()
        {
            // Ignore picking so the overlay never swallows pointer input meant for the game.
            var label = new Label { name = LABEL_NAME, pickingMode = PickingMode.Ignore };

            // Sizes are in points of the 1080x1920 reference PanelSettings_DebugHud scales from. Its
            // match of 0.5 interpolates linearly, so landscape 1920x1080 renders them about 17%
            // larger than portrait 1080x1920.
            label.style.position        = Position.Absolute;
            label.style.left            = 16f;
            label.style.top             = 16f;
            label.style.paddingLeft     = 12f;
            label.style.paddingRight    = 12f;
            label.style.paddingTop      = 6f;
            label.style.paddingBottom   = 6f;
            label.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            label.style.color           = Color.white;
            label.style.fontSize        = 32f;

            return label;
        }

        private void Refresh()
        {
            _label.text = _model.Text;
        }

        #endregion
    }
}
