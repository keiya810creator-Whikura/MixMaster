using System;
using System.Collections;
using MixMaster.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MixMaster.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerDeathPanelUI : MonoBehaviour
    {
        private const string ResourcePath = "UI/ScenePanels/PlayerDeathPanel";
        private const float NoticeSeconds = 4f;

        [SerializeField] private GameObject decision;
        [SerializeField] private TMP_Text lossText;
        [SerializeField] private Button returnButton;
        [SerializeField] private Button adReviveButton;
        [SerializeField] private Button adReturnButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject returnNotice;
        [SerializeField] private TMP_Text noticeText;

        private Action onReturn;
        private Action onAdRevive;
        private Action onAdReturn;
        private Coroutine noticeRoutine;

        internal bool IsDecisionVisible => isActiveAndEnabled && decision != null && decision.activeInHierarchy;

        internal static PlayerDeathPanelUI FindOrCreate(Scene scene)
        {
            foreach (var panel in FindObjectsByType<PlayerDeathPanelUI>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (panel.gameObject.scene == scene && panel.enabled && panel.HasReferences() &&
                    (panel.transform.parent == null || panel.transform.parent.gameObject.activeInHierarchy))
                    return panel;
            }

            var asset = Resources.Load<GameObject>(ResourcePath);
            var prefab = asset != null ? asset.GetComponent<PlayerDeathPanelUI>() : null;
            if (prefab == null || !prefab.HasReferences()) return null;
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.gameObject.scene != scene || !canvas.isRootCanvas ||
                    !canvas.isActiveAndEnabled || canvas.renderMode == RenderMode.WorldSpace)
                    continue;

                var safeArea = canvas.transform.Find("SafeArea") ?? canvas.transform.Find("SafeAreaHUD");
                if (safeArea != null && safeArea.gameObject.activeInHierarchy)
                    return Instantiate(prefab, safeArea, false);
                if (canvas.name == "Canvas" || canvas.name == "Cacvas")
                    return Instantiate(prefab, canvas.transform, false);
            }

            var instance = Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(instance.gameObject, scene);
            return instance;
        }

        private bool HasReferences() => decision != null && lossText != null &&
            returnButton != null && adReviveButton != null && adReturnButton != null &&
            statusText != null && returnNotice != null && noticeText != null;

        private void Awake()
        {
            if (returnButton != null) returnButton.onClick.AddListener(OnReturnClicked);
            if (adReviveButton != null) adReviveButton.onClick.AddListener(OnAdReviveClicked);
            if (adReturnButton != null) adReturnButton.onClick.AddListener(OnAdReturnClicked);
        }

        private void OnDestroy()
        {
            if (returnButton != null) returnButton.onClick.RemoveListener(OnReturnClicked);
            if (adReviveButton != null) adReviveButton.onClick.RemoveListener(OnAdReviveClicked);
            if (adReturnButton != null) adReturnButton.onClick.RemoveListener(OnAdReturnClicked);
        }

        private void OnReturnClicked() => onReturn?.Invoke();
        private void OnAdReviveClicked() => onAdRevive?.Invoke();
        private void OnAdReturnClicked() => onAdReturn?.Invoke();

        internal void ShowDecision(long lostGold, Action returnToTown, Action adRevive, Action adReturn)
        {
            gameObject.SetActive(true);
            if (noticeRoutine != null)
            {
                StopCoroutine(noticeRoutine);
                noticeRoutine = null;
            }

            returnNotice.SetActive(false);
            onReturn = returnToTown;
            onAdRevive = adRevive;
            onAdReturn = adReturn;
            SetLabel(adReviveButton, RewardedAdManager.CanSkip ? "その場で復活" : "広告を見て復活");
            SetLabel(adReturnButton, RewardedAdManager.CanSkip ? "ペナルティなしで街へ帰還" : "広告を見て街へ帰還\n（ペナルティなし）");
            ShowProjectedLoss(lostGold);
            ShowStatus(string.Empty);
            decision.SetActive(true);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(returnButton.gameObject);
        }

        private static void SetLabel(Button button, string text)
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = text;
        }

        internal void ShowProjectedLoss(long lostGold)
        {
            lossText.text = $"街へ帰還すると所持金の半分\n（{lostGold:N0} G）を失います。";
        }

        internal void SetActionsEnabled(bool enabled)
        {
            returnButton.interactable = enabled;
            adReviveButton.interactable = enabled;
            adReturnButton.interactable = enabled;
        }

        internal void ShowStatus(string text) => statusText.text = text;

        internal void HideDecision()
        {
            onReturn = null;
            onAdRevive = null;
            onAdReturn = null;
            var events = EventSystem.current;
            if (events != null && decision != null && events.currentSelectedGameObject != null &&
                events.currentSelectedGameObject.transform.IsChildOf(decision.transform))
                events.SetSelectedGameObject(null);
            if (decision != null) decision.SetActive(false);
        }

        internal void ShowOutcomeNotice(string message)
        {
            HideDecision();
            if (!isActiveAndEnabled) return;
            noticeText.text = message;
            returnNotice.SetActive(true);
            noticeRoutine = StartCoroutine(DismissNotice());
        }

        private IEnumerator DismissNotice()
        {
            yield return new WaitForSecondsRealtime(NoticeSeconds);
            returnNotice.SetActive(false);
            noticeRoutine = null;
        }
    }
}
