using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EXOD.Walk.EditorTools
{
    /// <summary>
    /// 메뉴 EXOD ▸ Stage 2 산책 ▸ 전화 부스 ▸ 기믹 배치 (산책 씬)
    /// 지금 있는 Stage2_Walk 씬을 새로 만들지 않고, 필요한 것만 "추가"한다. 여러 번 실행해도 이미 있는 것은 건드리지 않는다.
    ///  - 아이템 에셋 5개 (Assets/Stage2Walk/Items): 부스 열쇠, 절연 테이프, 다이얼 버튼 [1]·[2], 주소지
    ///  - Interactables/전화 부스 (잠긴 버전·열린 버전 오브젝트 + 부스 안 클로즈업)
    ///  - 벤치(테이프), 맨홀 1(버튼 [1]), 물웅덩이(버튼 [2])에 WalkItemPickup
    ///  - 가로등 1~4에 관리 번호판(PhoneBoothLamp)
    ///
    /// 프로젝트에 처음 들어오면(이 스크립트가 처음 컴파일되면) 한 번 자동으로 실행된다.
    /// 저장 안 한 씬이 있거나 플레이 중이면 자동 실행을 미루고, 메뉴로 직접 실행할 수 있다.
    /// </summary>
    [InitializeOnLoad]
    public static class PhoneBoothSetup
    {
        const string Root = "Assets/EXODStage2Walk";   // [통합] 통합 프로젝트의 산책 폴더
        const string WalkScenePath = Root + "/Scenes/Stage2_Walk.unity";
        const string ItemsFolder = Root + "/Items";
        const string PlaceholderSpritePath = Root + "/Placeholder/white.png";
        const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        const string BoothName = "전화 부스";
        static readonly Vector3 BoothPosition = new Vector3(11.5f, -0.6f, 0f);   // 가로등 3과 맨홀 2 사이
        static readonly Vector2 BoothSize = new Vector2(2.2f, 4.4f);

        static string AutoDoneKey => "EXOD_PhoneBooth_AutoSetupDone_" + Application.dataPath;
        static string PlatesDoneKey => "EXOD_PhoneBooth_LampPlatesV2_" + Application.dataPath;   // 번호판을 배경에 맞춘 버전
        const string PlatesFolder = Root + "/Art/LampPlates";
        const string PlateChildName = "관리 번호판";
        // 배경 그림의 기둥 가운데에 맞춘 위치 (가로등 판정 영역 가운데 기준, 가로등 1~4)
        static readonly float[] PlateOffsetX = { 0.033f, 0f, 0.085f, 0.048f };
        const float PlateOffsetY = -0.63f;
        static string InteriorArtKey => "EXOD_PhoneBooth_InteriorArtV1_" + Application.dataPath;
        const string InteriorArtFolder = Root + "/Art/BoothInterior";

        // [통합] 통합 프로젝트에서는 자동 배치를 하지 않습니다. (산책 씬 배치가 단독 프로젝트와 달라서 메뉴로만 실행)
        static PhoneBoothSetup()
        {
        }

        static void TryAutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryAutoSetup;
                return;
            }
            if (!File.Exists(WalkScenePath)) return;

            // 이미 배치돼 있으면(아이템이 있으면) 전체 배치는 건너뛴다
            if (!EditorPrefs.GetBool(AutoDoneKey, false) && File.Exists($"{ItemsFolder}/PhoneBooth_Key.asset"))
                EditorPrefs.SetBool(AutoDoneKey, true);
            bool needFull = !EditorPrefs.GetBool(AutoDoneKey, false);
            bool needPlates = !EditorPrefs.GetBool(PlatesDoneKey, false);
            bool needArt = !EditorPrefs.GetBool(InteriorArtKey, false);
            if (!needFull && !needPlates && !needArt) return;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.Log("[PhoneBooth] 저장하지 않은 씬이 있어서 자동 배치를 건너뜁니다. 메뉴 EXOD ▸ Stage 2 산책 ▸ 전화 부스 ▸ 기믹 배치 (산책 씬) 으로 실행하세요.");
                    return;
                }
            }

            string previousScene = SceneManager.GetActiveScene().path;
            EditorPrefs.SetBool(AutoDoneKey, true);
            EditorPrefs.SetBool(PlatesDoneKey, true);
            EditorPrefs.SetBool(InteriorArtKey, true);
            if (needFull) Run(false);
            else RunUpdates(needPlates, needArt);
            if (!string.IsNullOrEmpty(previousScene) && previousScene != WalkScenePath && File.Exists(previousScene))
                EditorSceneManager.OpenScene(previousScene);
        }

        /// <summary>전화 부스 배치를 이미 한 프로젝트에 새로 생긴 것만 더한다 (가로등 번호판, 부스 안 그림)</summary>
        static void RunUpdates(bool plates, bool art)
        {
            Scene scene = EditorSceneManager.OpenScene(WalkScenePath, OpenSceneMode.Single);
            Transform interactables = FindRoot(scene, "Interactables");
            if (interactables == null) return;
            var report = new List<string>();
            if (plates) for (int i = 1; i <= 4; i++) AddPlate(interactables, "가로등 " + i, i, report);
            if (art) AssignInteriorArt(interactables, report);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PhoneBooth] 전화 부스 업데이트 완료 (자동)\n" + string.Join("\n", report));
        }

        [MenuItem("EXOD/Stage 2 산책/전화 부스/기믹 배치 (산책 씬)")]
        public static void Setup() => Run(true);

        static void Run(bool interactive)
        {
            if (EditorApplication.isPlaying)
            {
                if (interactive) EditorUtility.DisplayDialog("전화 부스", "플레이 모드를 끈 다음 실행하세요.", "확인");
                return;
            }
            if (!File.Exists(WalkScenePath))
            {
                if (interactive) EditorUtility.DisplayDialog("전화 부스", $"{WalkScenePath} 씬이 없습니다.", "확인");
                return;
            }
            if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(WalkScenePath, OpenSceneMode.Single);
            var report = new List<string>();

            // 1) 아이템
            EnsureFolder(ItemsFolder);
            ItemData key = EnsureItem("PhoneBooth_Key", "stage2_booth_key", "부스 열쇠", true, report);
            ItemData tape = EnsureItem("PhoneBooth_Tape", "stage2_insulating_tape", "절연 테이프", true, report);
            ItemData button1 = EnsureItem("PhoneBooth_DialButton1", "stage2_dial_button_1", "다이얼 버튼 [1]", true, report);
            ItemData button2 = EnsureItem("PhoneBooth_DialButton2", "stage2_dial_button_2", "다이얼 버튼 [2]", true, report);
            ItemData address = EnsureItem("PhoneBooth_Address", "stage2_address_note", "주소지", false, report);

            // 2) 부스
            Transform interactables = FindRoot(scene, "Interactables");
            if (interactables == null)
            {
                interactables = new GameObject("Interactables").transform;
                SceneManager.MoveGameObjectToScene(interactables.gameObject, scene);
            }

            Transform boothTf = interactables.Find(BoothName);
            if (boothTf == null)
            {
                boothTf = CreateBooth(interactables).transform;
                report.Add("전화 부스 오브젝트 생성 (잠긴 버전 / 열린 버전 / 부스 안)");
            }
            else report.Add("전화 부스는 이미 있어서 그대로 둠 (빈 아이템 칸만 채움)");

            var booth = boothTf.GetComponent<PhoneBooth>();
            if (booth == null) booth = boothTf.gameObject.AddComponent<PhoneBooth>();
            SetIfEmpty(booth, "boothKey", key);
            SetIfEmpty(booth, "insulatingTape", tape);
            SetIfEmpty(booth, "dialButton1", button1);
            SetIfEmpty(booth, "dialButton2", button2);
            SetIfEmpty(booth, "addressNote", address);
            SetIfEmpty(booth, "lockedVersion", FindChild(boothTf, "잠긴 버전"));
            SetIfEmpty(booth, "openVersion", FindChild(boothTf, "열린 버전"));
            SetIfEmpty(booth, "interior", boothTf.GetComponentInChildren<PhoneBoothInterior>(true));

            // 3) 아이템 숨긴 곳
            AddPickup(interactables, "벤치", tape, "(벤치 아래에 무언가 붙어 있다… 절연 테이프다.)", report);
            AddPickup(interactables, "맨홀 1", button1, "(맨홀 뚜껑 틈에 작은 버튼이 끼어 있다. 숫자 [1]이 적혀 있다.)", report);
            AddPickup(interactables, "물웅덩이", button2, "(물웅덩이 속에서 무언가 반짝인다. 숫자 [2]가 적힌 버튼이다.)", report);

            // 4) 가로등 번호판
            AddLamp(interactables, "가로등 1", 1, "[관할] 해월시 서림동", booth, report);
            AddLamp(interactables, "가로등 2", 2, "[도로명] 은하로 3길", booth, report);
            AddLamp(interactables, "가로등 3", 3, "[위치] 27번지 앞", booth, report);
            AddLamp(interactables, "가로등 4", 4, "[관리번호] 0412-B   (고장 신고 ☎ 120)", booth, report);
            for (int i = 1; i <= 4; i++) AddPlate(interactables, "가로등 " + i, i, report);
            AssignInteriorArt(interactables, report);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            string msg = string.Join("\n", report);
            Debug.Log("[PhoneBooth] 전화 부스 기믹 배치 완료" + (interactive ? "" : " (자동)") + "\n" + msg);
            if (!interactive) return;

            Selection.activeGameObject = boothTf.gameObject;
            EditorGUIUtility.PingObject(boothTf.gameObject);
            EditorUtility.DisplayDialog("전화 부스 기믹 배치 완료",
                msg + "\n\n이미지는 '전화 부스' 아래 '잠긴 버전' / '열린 버전'의 SpriteRenderer에 넣으면 됩니다.", "확인");
        }

        [MenuItem("EXOD/Stage 2 산책/전화 부스/(플레이 중) 기믹 아이템 전부 받기")]
        static void GiveAll()
        {
            if (!EditorApplication.isPlaying || PhoneBooth.Current == null)
            {
                EditorUtility.DisplayDialog("전화 부스", "산책 중(플레이 모드, 산책 씬)에 실행하세요.", "확인");
                return;
            }
            PhoneBooth.Current.GiveAllItemsForTest();
        }

        [MenuItem("EXOD/Stage 2 산책/전화 부스/(플레이 중) 진행 상황 보기")]
        static void LogProgress()
        {
            Debug.Log("[PhoneBooth] " + PhoneBoothProgress.Summary);
        }

        [MenuItem("EXOD/Stage 2 산책/전화 부스/(플레이 중) 진행 상황 초기화")]
        static void ResetProgress()
        {
            PhoneBoothProgress.ResetAll();
            if (PhoneBooth.Current != null) PhoneBooth.Current.RefreshLook();
            Debug.Log("[PhoneBooth] 진행 상황을 처음으로 되돌렸습니다.");
        }

        // ---------------- 부스 만들기 ----------------
        static GameObject CreateBooth(Transform parent)
        {
            Sprite white = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderSpritePath);
            Material unlit = LoadUnlitMaterial();

            var go = new GameObject(BoothName);
            go.transform.SetParent(parent, false);
            go.transform.position = BoothPosition;

            var box = go.AddComponent<BoxCollider2D>();
            box.size = BoothSize;

            var wi = go.AddComponent<WalkInteractable>();
            var wso = new SerializedObject(wi);
            wso.FindProperty("displayName").stringValue = "빨간 전화 부스";
            wso.ApplyModifiedPropertiesWithoutUndo();

            var lbl = go.AddComponent<WorldLabel>();
            var lso = new SerializedObject(lbl);
            lso.FindProperty("text").stringValue = "빨간 전화 부스";
            lso.FindProperty("offset").vector3Value = new Vector3(0f, BoothSize.y * 0.5f + 0.3f, 0f);
            lso.FindProperty("onlyWhenHovered").boolValue = true;
            lso.ApplyModifiedPropertiesWithoutUndo();

            // 잠긴 버전: 빨간 부스 + 노란 자물쇠
            var locked = MakeVersion(go.transform, "잠긴 버전", unlit);
            MakePlaceholder(locked.transform, "임시 모양 (이미지를 넣으면 플레이 때 숨겨짐)", white, unlit, Vector3.zero, BoothSize, new Color(0.78f, 0.06f, 0.08f), 1);
            MakePlaceholder(locked.transform, "임시 모양 - 유리", white, unlit, new Vector3(0f, 0.5f), new Vector2(1.6f, 2.4f), new Color(0.35f, 0.02f, 0.04f), 2);
            MakePlaceholder(locked.transform, "임시 모양 - 자물쇠", white, unlit, new Vector3(0.75f, -0.6f), new Vector2(0.3f, 0.4f), new Color(1f, 0.85f, 0.2f), 2);

            // 열린 버전: 빨간 부스 + 열린 문(안쪽이 보임)
            var open = MakeVersion(go.transform, "열린 버전", unlit);
            MakePlaceholder(open.transform, "임시 모양 (이미지를 넣으면 플레이 때 숨겨짐)", white, unlit, Vector3.zero, BoothSize, new Color(0.78f, 0.06f, 0.08f), 1);
            MakePlaceholder(open.transform, "임시 모양 - 열린 문", white, unlit, new Vector3(0f, -0.2f), new Vector2(1.6f, 3.6f), new Color(0.1f, 0.0f, 0.01f), 2);
            MakePlaceholder(open.transform, "임시 모양 - 전화기", white, unlit, new Vector3(-0.35f, 0.3f), new Vector2(0.5f, 0.8f), new Color(0.45f, 0.42f, 0.4f), 2);
            open.SetActive(false);

            // 부스 안 (클로즈업)
            var interior = new GameObject("부스 안 (클로즈업)");
            interior.transform.SetParent(go.transform, false);
            interior.AddComponent<PhoneBoothInterior>();

            go.AddComponent<PhoneBooth>();
            return go;
        }

        static GameObject MakeVersion(Transform parent, string name, Material unlit)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 3;   // 넣은 이미지가 임시 모양보다 앞에 보이게, 그녀(9)보다는 뒤
            if (unlit != null) sr.sharedMaterial = unlit;
            return go;
        }

        static void MakePlaceholder(Transform parent, string name, Sprite sprite, Material unlit, Vector3 localPos, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            if (unlit != null) sr.sharedMaterial = unlit;
        }

        // ---------------- 아이템 숨긴 곳 / 가로등 ----------------
        static void AddPickup(Transform interactables, string objectName, ItemData item, string line, List<string> report)
        {
            Transform t = interactables.Find(objectName);
            if (t == null) { report.Add($"⚠ '{objectName}'을(를) 찾지 못해 [{item.DisplayName}]을(를) 숨기지 못함"); return; }

            var pickup = t.GetComponent<WalkItemPickup>();
            if (pickup == null) pickup = t.gameObject.AddComponent<WalkItemPickup>();

            var so = new SerializedObject(pickup);
            var itemProp = so.FindProperty("item");
            if (itemProp.objectReferenceValue != null && itemProp.objectReferenceValue != item)
            {
                report.Add($"⚠ '{objectName}'에는 이미 다른 아이템({itemProp.objectReferenceValue.name})이 있어서 그대로 둠");
                return;
            }
            itemProp.objectReferenceValue = item;

            var lines = so.FindProperty("pickupLines");
            if (lines != null && lines.arraySize == 0)
            {
                lines.arraySize = 2;
                SetLine(lines.GetArrayElementAtIndex(0), "", line);
                SetLine(lines.GetArrayElementAtIndex(1), "", $"[{item.DisplayName}]을(를) 얻었다.");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            report.Add($"{objectName} → [{item.DisplayName}]");
        }

        static void AddLamp(Transform interactables, string objectName, int index, string plate, PhoneBooth booth, List<string> report)
        {
            Transform t = interactables.Find(objectName);
            if (t == null) { report.Add($"⚠ '{objectName}'을(를) 찾지 못함"); return; }

            var lamp = t.GetComponent<PhoneBoothLamp>();
            bool created = lamp == null;
            if (created) lamp = t.gameObject.AddComponent<PhoneBoothLamp>();

            var so = new SerializedObject(lamp);
            if (so.FindProperty("booth").objectReferenceValue == null) so.FindProperty("booth").objectReferenceValue = booth;
            if (created)
            {
                so.FindProperty("lampIndex").intValue = index;
                var lines = so.FindProperty("plateLines");
                lines.arraySize = 2;
                SetLine(lines.GetArrayElementAtIndex(0), "", "(가로등 기둥에 관리 번호판이 붙어 있다.)");
                SetLine(lines.GetArrayElementAtIndex(1), "", plate);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            report.Add(created ? $"{objectName} → 번호판 {index}: {plate}" : $"{objectName}: 번호판이 이미 있어서 그대로 둠");
        }

        static void AddPlate(Transform interactables, string objectName, int index, List<string> report)
        {
            Transform lampTf = interactables.Find(objectName);
            if (lampTf == null) return;

            Transform plateTf = lampTf.Find(PlateChildName);
            bool created = plateTf == null;
            SpriteRenderer sr;
            if (created)
            {
                string path = $"{PlatesFolder}/Plate_Lamp{index}.png";
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) { report.Add($"⚠ {path} 그림을 찾지 못함"); return; }

                var go = new GameObject(PlateChildName);
                go.transform.SetParent(lampTf, false);
                plateTf = go.transform;
                sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                Material unlit = LoadUnlitMaterial();
                if (unlit != null) sr.sharedMaterial = unlit;
            }
            else sr = plateTf.GetComponent<SpriteRenderer>();

            // 배경의 기둥 가운데, 사람 눈높이쯤 (기둥보다 앞, 그녀(9)보다 뒤)
            int i = Mathf.Clamp(index - 1, 0, PlateOffsetX.Length - 1);
            plateTf.localPosition = new Vector3(PlateOffsetX[i], PlateOffsetY, 0f);
            if (sr != null) sr.sortingOrder = 2;

            var lamp = lampTf.GetComponent<PhoneBoothLamp>();
            if (lamp != null)
            {
                if (sr != null) SetIfEmpty(lamp, "plateRenderer", sr);
                SetIfEmpty(lamp, "closeupSprite", AssetDatabase.LoadAssetAtPath<Sprite>($"{PlatesFolder}/Plate_Lamp{index}_Closeup.png"));
            }
            report.Add(created ? $"{objectName} → 번호판 그림 Plate_Lamp{index}" : $"{objectName}: 번호판 위치·크게 보기 그림 맞춤");
        }

        /// <summary>부스 안 그림(배경·전화기·수화기·선·버튼)을 PhoneBoothInterior 빈 칸에 넣는다</summary>
        static void AssignInteriorArt(Transform interactables, List<string> report)
        {
            Transform boothTf = interactables.Find(BoothName);
            var interior = boothTf != null ? boothTf.GetComponentInChildren<PhoneBoothInterior>(true) : null;
            if (interior == null) { report.Add("⚠ 부스 안(PhoneBoothInterior)을 찾지 못해 그림을 넣지 못함"); return; }
            var pairs = new[]
            {
                ("background", "Booth_Interior_BG"), ("phoneBody", "Phone_Body"), ("handsetSprite", "Phone_Handset"),
                ("cordCutSprite", "Phone_Cord_Cut"), ("cordFixedSprite", "Phone_Cord_Fixed"),
                ("dialKeySprite", "Phone_Key"), ("missingKeySprite", "Phone_Key_Missing"),
            };
            int n = 0;
            foreach (var (prop, file) in pairs)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{InteriorArtFolder}/{file}.png");
                if (sprite == null) { report.Add($"⚠ {file}.png 를 찾지 못함"); continue; }
                SetIfEmpty(interior, prop, sprite);
                n++;
            }
            report.Add($"부스 안 그림 {n}개 연결");
        }

        static void SetLine(SerializedProperty element, string speaker, string text)
        {
            element.FindPropertyRelative("speaker").stringValue = speaker;
            element.FindPropertyRelative("text").stringValue = text;
        }

        // ---------------- 도우미 ----------------
        static ItemData EnsureItem(string fileName, string id, string displayName, bool consume, List<string> report)
        {
            string path = $"{ItemsFolder}/{fileName}.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null) return item;

            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, path);
            var so = new SerializedObject(item);
            so.FindProperty("itemId").stringValue = id;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("consumeOnSuccessfulUse").boolValue = consume;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            report.Add($"아이템 생성: {displayName} ({id})");
            return item;
        }

        static void SetIfEmpty(Object target, string property, Object value)
        {
            if (value == null) return;
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null || p.objectReferenceValue != null) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform FindRoot(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name) return go.transform;
            return null;
        }

        static GameObject FindChild(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            return t != null ? t.gameObject : null;
        }

        static Material LoadUnlitMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            if (m != null) return m;
            foreach (var guid in AssetDatabase.FindAssets("Sprite-Unlit-Default t:Material"))
            {
                m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m != null) return m;
            }
            return null;
        }

        static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
