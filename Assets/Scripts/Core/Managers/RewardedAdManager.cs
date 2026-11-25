using System;
#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
using System.Threading;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
#endif
using UnityEngine;

namespace MixMaster.Core
{
    [DisallowMultipleComponent]
    public sealed class RewardedAdManager : MonoBehaviour
    {
        public static RewardedAdManager Instance { get; private set; }

#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
#if UNITY_IOS
        private const string TestAdUnitId = "ca-app-pub-3940256099942544/1712485313";
#else
        private const string TestAdUnitId = "ca-app-pub-3940256099942544/5224354917";
#endif
        private const float RetryDelay = 10f;
        private const float CacheLifetime = 55f * 60f;
        private RewardedAd readyAd;
        private RewardedAd showingAd;
        private bool initialized;
        private bool loading;
        private float nextLoadAt;
        private float expiresAt;
        private float originalTimeScale;
        public bool IsShowing => showingAd != null;
#else
        public bool IsShowing => false;
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
            if (Instance != this) return;
            MobileAds.Initialize(status => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (this == null) return;
                initialized = status != null;
                if (initialized) LoadAd();
                else Debug.LogWarning("[AdMob] 初期化に失敗しました。", this);
            }));
#endif
        }

        private void Update()
        {
#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
            if (readyAd != null && Time.unscaledTime >= expiresAt)
            {
                readyAd.Destroy();
                readyAd = null;
            }
            if (readyAd == null) LoadAd();
#endif
        }

        // completed receives null only after the reward is earned and the ad closes.
        internal bool TryShow(Action<string> completed, out string message)
        {
#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
            if (IsShowing)
            {
                message = "広告を表示中です。";
                return false;
            }
            if (readyAd == null || Time.unscaledTime >= expiresAt || !readyAd.CanShowAd())
            {
                readyAd?.Destroy();
                readyAd = null;
                LoadAd();
                message = Time.unscaledTime < nextLoadAt
                    ? "広告を読み込めませんでした。時間をおいてお試しください。"
                    : "広告を準備中です。少し待ってからもう一度お試しください。";
                return false;
            }

            message = null;
            RewardedAd ad = readyAd;
            readyAd = null;
            showingAd = ad;
            originalTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            // Direct AdMob ads deliver the reward before dismissal. The reward
            // callback only sets a flag, as it may run on a native thread.
            int earned = 0;
            ad.OnAdFullScreenContentClosed += () => MobileAdsEventExecutor.ExecuteInUpdate(() =>
                Finish(ad, completed, Volatile.Read(ref earned) == 1 ? null : "広告の視聴が完了していません。"));
            ad.OnAdFullScreenContentFailed += error => MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                Debug.LogWarning("[AdMob] 表示に失敗しました: " + error);
                Finish(ad, completed, "広告を表示できませんでした。もう一度お試しください。");
            });

            try
            {
                ad.Show(_ => Interlocked.Exchange(ref earned, 1));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[AdMob] " + exception.Message, this);
                Finish(ad, completed, "広告を表示できませんでした。もう一度お試しください。");
            }
            return true;
#else
            message = "広告はAndroidとiOSで利用できます。";
            return false;
#endif
        }

#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
        private void LoadAd()
        {
            if (!initialized || loading || IsShowing || Time.unscaledTime < nextLoadAt) return;
            loading = true;
            RewardedAd.Load(TestAdUnitId, new AdRequest(), (ad, error) =>
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    if (this == null)
                    {
                        ad?.Destroy();
                        return;
                    }

                    loading = false;
                    if (error != null || ad == null)
                    {
                        ad?.Destroy();
                        nextLoadAt = Time.unscaledTime + RetryDelay;
                        Debug.LogWarning("[AdMob] 読み込みに失敗しました: " + error, this);
                        return;
                    }

                    readyAd = ad;
                    expiresAt = Time.unscaledTime + CacheLifetime;
                }));
        }

        private void Finish(RewardedAd ad, Action<string> completed, string message)
        {
            if (this == null || showingAd != ad) return;
            showingAd = null;
            Time.timeScale = originalTimeScale;
            ad.Destroy();
            try { completed(message); }
            finally { LoadAd(); }
        }
#endif

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
            if (IsShowing) Time.timeScale = originalTimeScale;
            showingAd?.Destroy();
            showingAd = null;
            readyAd?.Destroy();
            readyAd = null;
#endif
        }
    }
}
