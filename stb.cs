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

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            try
            {
                var harmony = new Harmony(HarmonyID);
                harmony.PatchAll();
                UnityEngine.Debug.Log("[ScrollThroughBlocker] Optimized Harmony hooks initialized successfully.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[ScrollThroughBlocker] Failed to initialize Harmony patches: " + ex.Message);
            }
        }

        private void Update()
        {
            ScrollBlockerManager.UpdateFrame();
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

    [HarmonyPatch(typeof(AxisBinding), nameof(AxisBinding.GetAxis))]
    public static class Patch_AxisBinding_GetAxis
    {
        [HarmonyPrefix]
        public static bool Prefix(AxisBinding __instance, ref float __result)
        {
            if (GameSettings.AXIS_MOUSEWHEEL != null && __instance == GameSettings.AXIS_MOUSEWHEEL)
            {
                if (ScrollBlockerManager.IsMouseOverUI)
                {
                    __result = 0f; 
                    return false; 
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetAxis), new Type[] { typeof(string) })]
    public static class Patch_Input_GetAxis
    {
        [HarmonyPrefix]
        public static bool Prefix(string axisName, ref float __result)
        {
            if (axisName == "Mouse ScrollWheel")
            {
                if (ScrollBlockerManager.IsMouseOverUI)
                {
                    __result = 0f;
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Input), "mouseScrollDelta", MethodType.Getter)]
    public static class Patch_Input_mouseScrollDelta
    {
        [HarmonyPrefix]
        public static bool Prefix(ref Vector2 __result)
        {
            if (ScrollBlockerManager.IsMouseOverUI)
            {
                __result = Vector2.zero;
                return false;
            }
            return true;
        }
    }
}