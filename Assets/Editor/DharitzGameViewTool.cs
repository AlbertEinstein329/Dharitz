using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cambia la resolucion de la ventana Game desde codigo para comprobar la UI en distintas pantallas moviles
/// (la API publica no lo permite). Se usa desde las pruebas: DharitzGameViewTool.SetResolution(1080, 1920).
/// </summary>
public static class DharitzGameViewTool
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public static void SetResolution(int width, int height)
    {
        Assembly editorAsm = typeof(Editor).Assembly;
        Type gameViewType = editorAsm.GetType("UnityEditor.GameView");
        Type sizesType = editorAsm.GetType("UnityEditor.GameViewSizes");
        Type sizeType = editorAsm.GetType("UnityEditor.GameViewSize");
        Type sizeTypeEnum = editorAsm.GetType("UnityEditor.GameViewSizeType");
        Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);

        object sizes = singletonType.GetProperty("instance").GetValue(null);
        object groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
        object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });

        string label = $"Dharitz {width}x{height}";
        int index = FindIndex(group, label);
        if (index < 0)
        {
            object fixedResolution = Enum.Parse(sizeTypeEnum, "FixedResolution");
            object size = Activator.CreateInstance(sizeType, new object[] { fixedResolution, width, height, label });
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            index = FindIndex(group, label);
        }

        var gameView = EditorWindow.GetWindow(gameViewType);
        gameViewType.GetMethod("SizeSelectionCallback").Invoke(gameView, new object[] { index, null });
        gameView.Repaint();
    }

    private static int FindIndex(object group, string label)
    {
        int total = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        MethodInfo getSize = group.GetType().GetMethod("GetGameViewSize");
        for (int i = 0; i < total; i++)
        {
            object size = getSize.Invoke(group, new object[] { i });
            string name = (string)size.GetType().GetProperty("baseText", All).GetValue(size);
            if (name == label) return i;
        }
        return -1;
    }
}
