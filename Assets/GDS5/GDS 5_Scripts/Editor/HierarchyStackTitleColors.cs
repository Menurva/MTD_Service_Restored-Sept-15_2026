using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class HierarchyStackTitleColors
{
    private static readonly Color DefaultBackground = new Color(0.24f, 0.25f, 0.33f, 1f);
    private static readonly Color PlayerBackground = new Color(0.07f, 0.38f, 0.38f, 1f);
    private static readonly Color CameraBackground = new Color(0.12f, 0.28f, 0.52f, 1f);
    private static readonly Color WorldBackground = new Color(0.15f, 0.36f, 0.18f, 1f);
    private static readonly Color CarBackground = new Color(0.47f, 0.25f, 0.08f, 1f);
    private static readonly Color TextColor = new Color(0.96f, 0.96f, 0.96f, 1f);

    static HierarchyStackTitleColors()
    {
        EditorApplication.hierarchyWindowItemOnGUI += DrawStackTitleRow;
    }

    private static void DrawStackTitleRow(int instanceId, Rect selectionRect)
    {
        var gameObject = EditorUtility.InstanceIDToObject(instanceId) as GameObject;

        if (gameObject == null || !IsStackTitle(gameObject))
        {
            return;
        }

        var backgroundColor = GetTitleColor(gameObject.name);
        var rowRect = new Rect(selectionRect.x, selectionRect.y, selectionRect.width, selectionRect.height);
        EditorGUI.DrawRect(rowRect, backgroundColor);

        var labelStyle = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            clipping = TextClipping.Clip,
        };
        labelStyle.normal.textColor = TextColor;

        GUI.Label(rowRect, CleanTitle(gameObject.name), labelStyle);
    }

    private static bool IsStackTitle(GameObject gameObject)
    {
        var cleanName = gameObject.name.Trim().Trim('\'', '"');
        var hasOnlyTransform = gameObject.GetComponents<Component>().Length == 1;

        return hasOnlyTransform && IsDecoratedTitle(cleanName);
    }

    private static bool IsDecoratedTitle(string name)
    {
        if (name.Length < 8)
        {
            return false;
        }

        return HasRepeatedMarker(name, '=') || HasRepeatedMarker(name, '+') || HasRepeatedMarker(name, '-');
    }

    private static bool HasRepeatedMarker(string name, char marker)
    {
        var repeatedMarker = new string(marker, 4);

        return name.StartsWith(repeatedMarker, StringComparison.Ordinal) ||
               name.EndsWith(repeatedMarker, StringComparison.Ordinal);
    }

    private static Color GetTitleColor(string title)
    {
        var upperTitle = title.ToUpperInvariant();

        if (upperTitle.Contains("PLAYER"))
        {
            return PlayerBackground;
        }

        if (upperTitle.Contains("CAMERA"))
        {
            return CameraBackground;
        }

        if (upperTitle.Contains("WORLD"))
        {
            return WorldBackground;
        }

        if (upperTitle.Contains("CAR"))
        {
            return CarBackground;
        }

        return DefaultBackground;
    }

    private static string CleanTitle(string title)
    {
        return title.Trim()
            .Trim('\'', '"')
            .Trim('=', '+', '-', ' ')
            .ToUpperInvariant();
    }
}
