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
        private const string LockID = "ScrollThroughBlocker_CameraLock";
        private bool isLockActive = false;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            try
            {
                var harmony = new Harmony(HarmonyID);
                harmony.PatchAll();
                UnityEngine.Debug.Log("[ScrollThroughBlocker] Initialized successfully.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[ScrollThroughBlocker] Harmony patch error: " + ex.Message);
            }
        }

        private void Update()
        {
            ScrollBlockerManager.UpdateFrame();

            if (ScrollBlockerManager.IsMouseOverUI)
            {
                if (!isLockActive)
                {
                    InputLockManager.SetControlLock(ControlTypes.CAMERACONTROLS, LockID);
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

        private void OnDestroy()
        {
            if (isLockActive)
            {
                InputLockManager.RemoveControlLock(LockID);
                isLockActive = false;
            }
        }
    }

    public static class ScrollBlockerManager
    {
        private static readonly List<Rect> recordedRects = new List<Rect>(32);
        private static readonly List<Rect> activeRects = new List<Rect>(32);

        private static int lastEvaluatedFrame = -1;
        private static bool cachedMouseOverUI = false;

        public static void RegisterWindowRect(Rect rect)
        {
            if (Event.current != null && Event.current.type != EventType.Layout)
                return;

            if (rect.width > 5 && rect.height > 5)
            {
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
