using System;
using System.Collections.Generic;
using UnityEngine;

namespace EXOD.Walk
{
    /// <summary>
    /// 전화 부스 기믹 진행 상황. 날이 바뀌어도, 산책 씬이 닫혔다 열려도 유지된다 (게임을 끄면 초기화).
    /// 들켜도 열쇠·아이템·수리 상태·확인한 가로등은 그대로 남는다.
    /// (통화 중에 들켰을 때만 수화기 선이 다시 끊긴다)
    /// </summary>
    public static class PhoneBoothProgress
    {
        /// <summary>부스 문을 열쇠로 열었는지</summary>
        public static bool BoothUnlocked;
        /// <summary>잘린 수화기 선을 테이프로 이었는지</summary>
        public static bool CordFixed;
        /// <summary>다이얼 버튼 [1]을 끼웠는지</summary>
        public static bool Button1Installed;
        /// <summary>다이얼 버튼 [2]를 끼웠는지</summary>
        public static bool Button2Installed;
        /// <summary>가로등 4개를 모두 보고 '주소지'를 얻었는지</summary>
        public static bool AddressObtained;
        /// <summary>112 신고를 끝까지 마쳤는지 (이후 하루가 끝나면 탈출 엔딩)</summary>
        public static bool Reported;
        /// <summary>탈출 엔딩이 이미 나왔는지</summary>
        public static bool EndingShown;

        /// <summary>번호판을 확인한 가로등 번호</summary>
        public static readonly HashSet<int> LampsChecked = new HashSet<int>();

        /// <summary>신고 성공 뒤 하루가 끝나 경찰이 도착했을 때 (탈출 엔딩). 팀 엔딩 씬 연결용.</summary>
        public static event Action EndingReached;

        public static bool PhoneRepaired => CordFixed && Button1Installed && Button2Installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => ResetAll();

        /// <summary>처음 상태로 되돌린다 (새 게임 시작 등)</summary>
        public static void ResetAll()
        {
            BoothUnlocked = false;
            CordFixed = false;
            Button1Installed = false;
            Button2Installed = false;
            AddressObtained = false;
            Reported = false;
            EndingShown = false;
            LampsChecked.Clear();
            EndingReached = null;
        }

        internal static void RaiseEnding()
        {
            EndingShown = true;
            EndingReached?.Invoke();
        }

        /// <summary>디버그 표시용 한 줄 요약</summary>
        public static string Summary =>
            $"부스 {(BoothUnlocked ? "열림" : "잠김")} / 선 {(CordFixed ? "O" : "X")} / [1] {(Button1Installed ? "O" : "X")} / [2] {(Button2Installed ? "O" : "X")} / " +
            $"가로등 {LampsChecked.Count}/4 / 주소지 {(AddressObtained ? "O" : "X")} / 신고 {(Reported ? "완료" : "-")}";
    }
}
