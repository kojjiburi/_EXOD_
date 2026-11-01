#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class MirrorMazeMenu
{
    // 하이어라키 우클릭 > EXOD > 거울 미로 기믹 (또는 상단 메뉴 GameObject > EXOD)
    [MenuItem("GameObject/EXOD/거울 미로 기믹 (Mirror Maze)", false, 10)]
    static void CreateGimmick(MenuCommand command)
    {
        var go = new GameObject("MirrorMazeGimmick");
        go.AddComponent<MirrorMazeGimmick>();
        GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create Mirror Maze Gimmick");
        Selection.activeObject = go;
    }
}
#endif
