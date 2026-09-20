using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using HarmonyLib;
using KSP.UI.Screens;

namespace ScrollThroughBlocker
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class ScrollThroughBlockerPlugin : MonoBehaviour
    {
        private const string HarmonyID = "com.extraext.scrollthroughblocker";
        private const string LockID = "ScrollThroughBlocker_Lock";
        private const string ScrollRectLockID = "ScrollThroughBlocker_ScrollRectLock";

        private const ControlTypes FullLockMask = ControlTypes.All;
        private const ControlTypes ScrollRectLockMask = ControlTypes.CAMERACONTROLS;

        private bool isLockActive = false;
        private bool isScrollRectLockActive = false;

        private ApplicationLauncherButton appLauncherButton;
        private Texture2D iconTexture;
        private bool showGUI = false;
        private Rect guiRect = new Rect(Screen.width - 250, 40, 240, 55);

        private readonly List<MonoBehaviour> wasdInstances = new List<MonoBehaviour>();
        private readonly HashSet<MonoBehaviour> wasdDisabledByUs = new HashSet<MonoBehaviour>();
        private bool wasdModInstalled = false;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            wasdModInstalled = DetectWasdModInstalled();

            ScrollBlockerManager.blockWasdInEditor = PlayerPrefs.GetInt("STB_BlockWasdEditor", 1) == 1;

            try
            {
                var harmony = new Harmony(HarmonyID);
                harmony.PatchAll();
                UnityEngine.Debug.Log("[ScrollThroughBlocker] Harmony patches initialized successfully.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[ScrollThroughBlocker] Harmony patch error: " + ex.ToString());
            }
            
            GameEvents.onLevelWasLoaded.Add(OnLevelLoaded);
            GameEvents.onGUIApplicationLauncherReady.Add(OnAppLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(OnAppLauncherDestroyed);
        }

        private bool DetectWasdModInstalled()
        {
            foreach (var loaded in AssemblyLoader.loadedAssemblies)
            {
                Type[] types;
                try
                {
                    types = loaded.assembly.GetTypes();
                }
                catch
                {
                    continue;
                }

                for (int i = 0; i < types.Length; i++)
                {
                    string tName = types[i].Name;
                    if (tName.IndexOf("Wasd", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        tName.IndexOf("Camera", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        tName.IndexOf("GUI", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void OnDestroy()
        {
            GameEvents.onLevelWasLoaded.Remove(OnLevelLoaded);
            GameEvents.onGUIApplicationLauncherReady.Remove(OnAppLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(OnAppLauncherDestroyed);

            if (appLauncherButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(appLauncherButton);
                appLauncherButton = null;
            }

            if (isLockActive)
            {
                InputLockManager.RemoveControlLock(LockID);
                isLockActive = false;
            }

            if (isScrollRectLockActive)
            {
                InputLockManager.RemoveControlLock(ScrollRectLockID);
                isScrollRectLockActive = false;
            }
        }

        private void OnLevelLoaded(GameScenes scene)
        {
            isLockActive = false;
            isScrollRectLockActive = false;
            wasdInstances.Clear();
            wasdDisabledByUs.Clear();
        }

        private void OnAppLauncherReady()
        {
            if (appLauncherButton == null && ApplicationLauncher.Ready)
            {
                iconTexture = GameDatabase.Instance.GetTexture("000_ScrollThroughBlocker/Icon/icon", false);
                
                appLauncherButton = ApplicationLauncher.Instance.AddModApplication(
                    onTrue: () => showGUI = true,
                    onFalse: () => showGUI = false,
                    onHover: null, onHoverOut: null, onEnable: null, onDisable: null,
                    visibleInScenes: ApplicationLauncher.AppScenes.ALWAYS,
                    texture: iconTexture
                );
            }
        }

        private void OnAppLauncherDestroyed()
        {
            if (appLauncherButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(appLauncherButton);
                appLauncherButton = null;
            }
        }

        private void OnGUI()
        {
            if (showGUI)
            {
                guiRect = GUILayout.Window(894562, guiRect, DrawGUI, "Scroll Through Blocker");
            }
        }

        private void DrawGUI(int windowID)
        {
            GUILayout.BeginVertical();
            GUILayout.Space(6);

            GUI.enabled = wasdModInstalled;
            bool previousState = ScrollBlockerManager.blockWasdInEditor;
            ScrollBlockerManager.blockWasdInEditor = GUILayout.Toggle(ScrollBlockerManager.blockWasdInEditor, " Block WASD camera pan in Editor");
            GUI.enabled = true;

            if (previousState != ScrollBlockerManager.blockWasdInEditor)
            {
                PlayerPrefs.SetInt("STB_BlockWasdEditor", ScrollBlockerManager.blockWasdInEditor ? 1 : 0);
                PlayerPrefs.Save();
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void Update()
        {
            ScrollBlockerManager.UpdateFrame();
            bool isHovering = ScrollBlockerManager.IsMouseOverUI;

            if (isHovering)
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

            bool overScrollRect = ScrollBlockerManager.IsMouseOverScrollRect;
            if (overScrollRect)
            {
                if (!isScrollRectLockActive)
                {
                    InputLockManager.SetControlLock(ScrollRectLockMask, ScrollRectLockID);
                    isScrollRectLockActive = true;
                }
            }
            else
            {
                if (isScrollRectLockActive)
                {
                    InputLockManager.RemoveControlLock(ScrollRectLockID);
                    isScrollRectLockActive = false;
                }
            }

            bool shouldBlockWasd = isHovering || overScrollRect;

            if (HighLogic.LoadedSceneIsEditor && ScrollBlockerManager.blockWasdInEditor)
            {
                if (wasdInstances.Count == 0 && Time.frameCount % 60 == 0)
                {
                    MonoBehaviour[] allBehaviors = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
                    for (int i = 0; i < allBehaviors.Length; i++)
                    {
                        string tName = allBehaviors[i].GetType().Name;
                        if (tName.IndexOf("Wasd", StringComparison.OrdinalIgnoreCase) >= 0 && 
                            tName.IndexOf("Camera", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            tName.IndexOf("GUI", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            wasdInstances.Add(allBehaviors[i]);
                        }
                    }
                }

                if (wasdInstances.Count > 0)
                {
                    if (shouldBlockWasd)
                    {
                        for (int i = 0; i < wasdInstances.Count; i++)
                        {
                            MonoBehaviour inst = wasdInstances[i];
                            if (inst != null && inst.enabled)
                            {
                                inst.enabled = false;
                                wasdDisabledByUs.Add(inst);
                            }
                        }
                    }
                    else if (wasdDisabledByUs.Count > 0)
                    {
                        foreach (MonoBehaviour inst in wasdDisabledByUs)
                        {
                            if (inst != null)
                                inst.enabled = true;
                        }
                        wasdDisabledByUs.Clear();
                    }
                }
            }
            else
            {
                if (wasdDisabledByUs.Count > 0)
                {
                    foreach (MonoBehaviour inst in wasdDisabledByUs)
                    {
                        if (inst != null)
                            inst.enabled = true;
                    }
                    wasdDisabledByUs.Clear();
                }
                wasdInstances.Clear();
            }
        }
    }

    public static class ScrollBlockerManager
    {
        public static bool blockNativeUI = false;
        public static bool blockWasdInEditor = true;

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
                    return cachedMouseOverUI;

                lastEvaluatedFrame = Time.frameCount;
                cachedMouseOverUI = CheckHover();
                return cachedMouseOverUI;
            }
        }

        private static bool CheckHover()
        {
            if (blockNativeUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return true;

            if (activeRects.Count == 0) return false;

            Vector2 guiMouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            for (int i = 0; i < activeRects.Count; i++)
            {
                if (activeRects[i].Contains(guiMouse))
                    return true;
            }

            return false;
        }

        private static readonly List<RaycastResult> scrollRectRaycastBuffer = new List<RaycastResult>(16);

        public static bool IsMouseOverScrollRect
        {
            get
            {
                if (EventSystem.current == null) return false;

                scrollRectRaycastBuffer.Clear();
                var pointerData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
                EventSystem.current.RaycastAll(pointerData, scrollRectRaycastBuffer);

                for (int i = 0; i < scrollRectRaycastBuffer.Count; i++)
                {
                    if (scrollRectRaycastBuffer[i].gameObject.GetComponentInParent<ScrollRect>() != null)
                        return true;
                }

                return false;
            }
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
