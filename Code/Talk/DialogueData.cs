using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 대화 타입 정의
/// </summary>
public enum DialogueType
{
    Character,      // 인물 간 대사
    Interaction,    // 사물 상호작용 대사
    Narration       // 나레이션 (이름표 없음)
}

/// <summary>
/// 대화 한 줄 데이터
/// </summary>
[Serializable]
public class DialogueLine
{
    [Header("발화자 정보")]
    public string speakerName;          // 이름표에 표시될 이름 (빈칸이면 이름표 숨김)

    [Header("대사 내용")]
    [TextArea(2, 5)]
    public string text;                 // 대화 내용

    [Header("타입")]
    public DialogueType dialogueType = DialogueType.Character;

    [Header("타이핑 속도 (0 = 기본값 사용)")]
    public float typingSpeed = 0f;
}

/// <summary>
/// 대화 묶음 (ScriptableObject로 에디터에서 관리)
/// </summary>
[CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    [Header("대화 ID (고유값)")]
    public string dialogueID;

    [Header("대화 타입")]
    public DialogueType dialogueType = DialogueType.Character;

    [Header("대화 목록")]
    public List<DialogueLine> lines = new List<DialogueLine>();
}