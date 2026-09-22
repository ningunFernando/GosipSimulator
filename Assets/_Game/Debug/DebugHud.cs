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
    ///
    /// Since milestone 11 it is also where the village becomes visible: opinions, the rumors
    /// carrying them and what the shop charges, all as copies pushed by the modules that own them.
    /// It is the only runtime consumer of OnRumorSpread and of three shop events, so in a release
    /// build, where this assembly does not compile, EventBus reports them as published to nobody.
    /// That warning is true and stays until a player-facing UI listens (A1).
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
            EventBus.Subscribe<OnRelationshipChanged>(HandleRelationshipChanged);
            EventBus.Subscribe<OnRelationshipsRestored>(HandleRelationshipsRestored);
            EventBus.Subscribe<OnRumorSpread>(HandleRumorSpread);
            EventBus.Subscribe<OnShopTermsChanged>(HandleShopTermsChanged);
            EventBus.Subscribe<OnPurchaseSettled>(HandlePurchaseSettled);
            EventBus.Subscribe<OnPurchaseRefused>(HandlePurchaseRefused);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<OnBootstrapComplete>(HandleBootstrapComplete);
            EventBus.Unsubscribe<OnGameStateChanged>(HandleGameStateChanged);
            EventBus.Unsubscribe<OnProgressChanged>(HandleProgressChanged);
            EventBus.Unsubscribe<OnRelationshipChanged>(HandleRelationshipChanged);
            EventBus.Unsubscribe<OnRelationshipsRestored>(HandleRelationshipsRestored);
            EventBus.Unsubscribe<OnRumorSpread>(HandleRumorSpread);
            EventBus.Unsubscribe<OnShopTermsChanged>(HandleShopTermsChanged);
            EventBus.Unsubscribe<OnPurchaseSettled>(HandlePurchaseSettled);
            EventBus.Unsubscribe<OnPurchaseRefused>(HandlePurchaseRefused);

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

        private void HandleRelationshipChanged(OnRelationshipChanged e)
        {
            _model.SetOpinion(e.npcId, e.aboutId, e.current, e.reason);
            Refresh();
        }

        private void HandleRelationshipsRestored(OnRelationshipsRestored e)
        {
            _model.RestoreOpinions(e.npcIds, e.aboutIds, e.values);
            Refresh();
        }

        private void HandleRumorSpread(OnRumorSpread e)
        {
            _model.AddRumor(e.actionId, e.fromId, e.toId, e.hop, e.weight);
            Refresh();
        }

        private void HandleShopTermsChanged(OnShopTermsChanged e)
        {
            _model.SetShopTerms(e.shopkeeperId, e.price, e.pricePercent, e.refuses);
            Refresh();
        }

        private void HandlePurchaseSettled(OnPurchaseSettled e)
        {
            _model.RecordPurchaseSettled(e.shopkeeperId, e.itemId, e.price, e.paid);
            Refresh();
        }

        private void HandlePurchaseRefused(OnPurchaseRefused e)
        {
            _model.RecordPurchaseRefused(e.shopkeeperId, e.itemId, e.opinion);
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
            // 24 rather than the template's 32: the village adds a dozen lines, and at 32 they would
            // cover most of a landscape screen.
            label.style.fontSize        = 24f;

            return label;
        }

        private void Refresh()
        {
            _label.text = _model.Text;
        }

        #endregion
    }
}
