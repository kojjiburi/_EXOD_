/// <summary>
/// 탈출 엔딩 컷신에서 "…수갑을 채웠다" 대사가 뜰 때 수갑 소리 (사용자 요청 2026-11-03, 문 삐걱 소리와 겹쳐도 됨)
/// 탈출 엔딩 컷신을 틀기 직전에 ArmHandcuffs()를 부름. 경찰차 사이렌은 EndingScreen(탈출 엔딩 화면)에서.
/// </summary>
public static class EscapeEndingSfx
{
    private static DialogueManager hooked;

    public static void ArmHandcuffs()
    {
        Disarm();
        hooked = DialogueManager.Instance;
        if (hooked != null)
            hooked.LineShown += OnLine;
    }

    private static void OnLine(DialogueLine line)
    {
        if (line == null || line.text == null || !line.text.Contains("수갑"))
            return;
        EXODSound.Play("handcuffs");
        Disarm();
    }

    private static void Disarm()
    {
        if (hooked != null)
            hooked.LineShown -= OnLine;
        hooked = null;
    }
}
