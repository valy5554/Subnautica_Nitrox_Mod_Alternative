using System.Reflection;
using System;
using UnityEngine;
using UMathf = UnityEngine.Mathf;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class CyclopsHelmHUDManager_Update_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((CyclopsHelmHUDManager t) => t.Update());

    public static void Postfix(CyclopsHelmHUDManager __instance)
    {
        // To show the Cyclops HUD every time "hudActive" have to be true. "hornObject" is a good indicator to check if the player piloting the cyclops.
        if (!__instance.hornObject.activeSelf && __instance.hudActive)
        {
            __instance.canvasGroup.interactable = false;
        }
        else if (!__instance.hudActive)
        {
            __instance.hudActive = true;
        }
        if (__instance.subLiveMixin.IsAlive())
        {
            if (__instance.motorMode.engineOn)
            {
                __instance.engineToggleAnimator.SetTrigger("EngineOn");
            }
            else
            {
                __instance.engineToggleAnimator.SetTrigger("EngineOff");
            }
        }

        // UI sync: the propeller animation follows motorMode, but the HUD buttons often update only on local click.
        // Force the button highlight/toggle state to match the current motor mode for remote pilots.
        TrySyncMotorModeButtons(__instance);
    }

    private static void TrySyncMotorModeButtons(CyclopsHelmHUDManager hud)
    {
        if (hud == null || hud.motorMode == null || hud.subRoot == null)
        {
            return;
        }

        // Don't fight the local player while they are actively piloting.
        if (Player.main != null && Player.main.isPiloting && Player.main.currentSub == hud.subRoot)
        {
            return;
        }

        int currentMode = UMathf.Clamp((int)hud.motorMode.cyclopsMotorMode, 0, 2);

        Component[] buttons = hud.subRoot.GetComponentsInChildren(Type.GetType("CyclopsMotorModeButton, Assembly-CSharp") ?? typeof(Component), true);
        if (buttons == null || buttons.Length == 0)
        {
            return;
        }

        foreach (Component c in buttons)
        {
            if (c == null || c.GetType().Name != "CyclopsMotorModeButton")
            {
                continue;
            }

            int buttonMode = ReadButtonModeIndex(c);
            if (buttonMode < 0)
            {
                continue;
            }
            bool shouldBeSelected = buttonMode == currentMode;

            // Best-effort: set known boolean fields/properties or call a refresh method if present.
            SetBoolMemberIfExists(c, shouldBeSelected, "active", "isActive", "selected", "isSelected", "toggled", "isOn", "highlighted");
            CallRefreshIfExists(c, "UpdateText", "UpdateVisuals", "Refresh", "UpdateState", "UpdateMode");
        }
    }

    private static int ReadButtonModeIndex(Component button)
    {
        Type t = button.GetType();
        // Try common member names first.
        foreach (string name in new[] { "mode", "motorMode", "index", "modeIndex", "throttleIndex" })
        {
            FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null)
            {
                object v = f.GetValue(button);
                if (v is int i) return UMathf.Clamp(i, 0, 2);
                if (v is byte b) return UMathf.Clamp(b, 0, 2);
                if (v is Enum e) return UMathf.Clamp(Convert.ToInt32(e), 0, 2);
            }
            PropertyInfo p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanRead)
            {
                object v = p.GetValue(button, null);
                if (v is int i) return UMathf.Clamp(i, 0, 2);
                if (v is byte b) return UMathf.Clamp(b, 0, 2);
                if (v is Enum e) return UMathf.Clamp(Convert.ToInt32(e), 0, 2);
            }
        }

        // Fallback: find the first enum/int-like field that looks like a motor mode.
        FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (FieldInfo f in fields)
        {
            string n = f.Name.ToLowerInvariant();
            if (!n.Contains("mode"))
            {
                continue;
            }
            object v = f.GetValue(button);
            if (v is int i) return UMathf.Clamp(i, 0, 2);
            if (v is byte b) return UMathf.Clamp(b, 0, 2);
            if (v is Enum e) return UMathf.Clamp(Convert.ToInt32(e), 0, 2);
        }
        return -1;
    }

    private static void SetBoolMemberIfExists(Component obj, bool value, params string[] candidateNames)
    {
        Type t = obj.GetType();
        foreach (string name in candidateNames)
        {
            FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(bool))
            {
                f.SetValue(obj, value);
                return;
            }
            PropertyInfo p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanWrite && p.PropertyType == typeof(bool))
            {
                p.SetValue(obj, value, null);
                return;
            }
        }
    }

    private static void CallRefreshIfExists(Component obj, params string[] candidateMethodNames)
    {
        Type t = obj.GetType();
        foreach (string name in candidateMethodNames)
        {
            MethodInfo m = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (m != null)
            {
                m.Invoke(obj, null);
                return;
            }
        }
    }
}
