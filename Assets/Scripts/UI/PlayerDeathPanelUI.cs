using System;
using System.Collections;
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
        [SerializeField] private GameObject returnNotice;
        [SerializeField] private TMP_Text noticeText;

        private Action onReturn;
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
            returnButton != null && returnNotice != null && noticeText != null;

        private void Awake()
        {
            if (returnButton != null) returnButton.onClick.AddListener(OnReturnClicked);
        }

        private void OnDestroy()
        {
            if (returnButton != null) returnButton.onClick.RemoveListener(OnReturnClicked);
        }

        private void OnReturnClicked() => onReturn?.Invoke();

        internal void ShowDecision(long lostGold, Action returnToTown)
        {
            gameObject.SetActive(true);
            if (noticeRoutine != null)
            {
                StopCoroutine(noticeRoutine);
                noticeRoutine = null;
            }

            returnNotice.SetActive(false);
            onReturn = returnToTown;
            ShowProjectedLoss(lostGold);
            decision.SetActive(true);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(returnButton.gameObject);
        }

        internal void ShowProjectedLoss(long lostGold)
        {
            lossText.text = $"街へ帰還すると所持金の半分\n（{lostGold:N0} G）を失います。";
        }

        internal void ShowReturnFailed()
        {
            lossText.text = "街への帰還に失敗しました。\n所持金は減っていません。";
        }

        internal void HideDecision()
        {
            onReturn = null;
            var events = EventSystem.current;
            if (events != null && returnButton != null &&
                events.currentSelectedGameObject == returnButton.gameObject)
                events.SetSelectedGameObject(null);
            if (decision != null) decision.SetActive(false);
        }

        internal void ShowReturnNotice(long lostGold)
        {
            HideDecision();
            if (!isActiveAndEnabled) return;
            noticeText.text = $"街に帰還しました\n{lostGold:N0} Gを失いました。";
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
