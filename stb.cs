using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using HarmonyLib;

namespace ScrollThroughBlocker
{

    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class ScrollThroughBlockerPlugin : MonoBehaviour
    {
        private const string HarmonyID = "com.extraext.scrollthroughblocker";
        private const string LockID = "ScrollThroughBlocker_Lock";
        private const ControlTypes FullLockMask = ControlTypes.CAMERACONTROLS;

        private bool isLockActive = false;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            try
            {
                var harmony = new Harmony(HarmonyID);
                harmony.PatchAll();
                UnityEngine.Debug.Log("[ScrollThroughBlocker] Harmony patches initialized.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[ScrollThroughBlocker] Harmony patch error: " + ex.Message);
            }
            GameEvents.onLevelWasLoaded.Add(OnLevelLoaded);
        }

        private void OnDestroy()
        {
            GameEvents.onLevelWasLoaded.Remove(OnLevelLoaded);

            if (isLockActive)
            {
                InputLockManager.RemoveControlLock(LockID);
                isLockActive = false;
            }
        }

        private void OnLevelLoaded(GameScenes scene)
        {
            isLockActive = false;
        }

        private void Update()
        {
            ScrollBlockerManager.UpdateFrame();

            if (ScrollBlockerManager.IsMouseOverUI)
            {
                if (!isLockActive || InputLockManager.GetControlLock(LockID) == ControlTypes.None)
                {
                    InputLockManager.SetControlLock(FullLockMask, LockID);
                    isLockActive = true;
                }
            }
            else
            {
                if (isLockActive)
                {
                    InputLockManager.RemoveControlLock(LockID);
                    isLockActive = false;
                }
            }
        }
    }

    public static class ScrollBlockerManager
    {
        private static readonly List<Rect> recordedRects = new List<Rect>(64);
        private static readonly List<Rect> activeRects = new List<Rect>(64);

        private static int lastEvaluatedFrame = -1;
        private static bool cachedMouseOverUI = false;

        public static void RegisterWindowRect(Rect rect)
        {
            if (rect.width > 5 && rect.height > 5)
            {
                for (int i = 0; i < recordedRects.Count; i++)
                {
                    if (recordedRects[i].x == rect.x && 
                        recordedRects[i].y == rect.y && 
                        recordedRects[i].width == rect.width && 
                        recordedRects[i].height == rect.height)
                        return;
                }
                recordedRects.Add(rect);
            }
        }

        public static void UpdateFrame()
        {
            activeRects.Clear();
            if (recordedRects.Count > 0)
            {
                activeRects.AddRange(recordedRects);
                recordedRects.Clear();
            }

            lastEvaluatedFrame = -1;
        }

        public static bool IsMouseOverUI
        {
            get
            {
                if (Time.frameCount == lastEvaluatedFrame)
                {
                    return cachedMouseOverUI;
                }

                lastEvaluatedFrame = Time.frameCount;
                cachedMouseOverUI = CheckHover();
                return cachedMouseOverUI;
            }
        }

        private static bool CheckHover()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return true;
            }

            if (activeRects.Count == 0) return false;

            Vector2 guiMouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            for (int i = 0; i < activeRects.Count; i++)
            {
                if (activeRects[i].Contains(guiMouse))
                {
                    return true;
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(GUI), "DoWindow")]
    public static class Patch_GUIDoWindow
    {
        [HarmonyPrefix]
        public static void Prefix(Rect clientRect)
        {
            ScrollBlockerManager.RegisterWindowRect(clientRect);
        }
    }
}
