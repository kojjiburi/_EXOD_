using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EXOD.Walk.EditorTools
{
    /// <summary>
    /// 메뉴 EXOD ▸ Stage 2 산책 ▸ 테스트 씬 생성
    /// 임시 도형으로 된 집 씬(Stage2_Home)과 산책 씬(Stage2_Walk)을 만들고 빌드 씬 목록에 등록한다.
    /// 프로젝트를 처음 열었을 때 씬이 없으면 자동으로 한 번 만든다.
    /// </summary>
    [InitializeOnLoad]
    public static class Stage2WalkSceneBuilder
    {
        const string Root = "Assets/Stage2Walk";
        const string SceneFolder = Root + "/Scenes";
        const string HomeScenePath = SceneFolder + "/Stage2_Home.unity";
        const string WalkScenePath = SceneFolder + "/Stage2_Walk.unity";
        const string SpritePath = Root + "/Placeholder/white.png";
        const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        const string BackgroundPath = Root + "/Art/Bg_NightStreet.png";
        const float BackgroundPPU = 108f; // 1080px 높이 = 10유닛 = 카메라 orthographicSize 5에 꽉 참
        const string AutoBuildKey = "EXOD_Stage2Walk_AutoBuildTried_v3";
        const int SceneVersion = 3;       // 산책 씬 구성이 바뀌면 올린다 → 자동으로 다시 생성
        static string VersionKey => "EXOD_Stage2Walk_SceneVersion_" + Application.dataPath;

        static Stage2WalkSceneBuilder()
        {
            EditorApplication.delayCall += TryAutoBuild;
        }

        static void TryAutoBuild()
        {
            if (SessionState.GetBool(AutoBuildKey, false)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                // 플레이 중이면 플레이가 끝난 뒤 다시 시도
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                return;
            }
            SessionState.SetBool(AutoBuildKey, true);
            bool missing = !File.Exists(HomeScenePath) || !File.Exists(WalkScenePath);
            bool outdated = EditorPrefs.GetInt(VersionKey, 1) < SceneVersion;
            if (!missing && !outdated) return;

            // 저장 안 된 씬이 있으면 건드리지 않고 메뉴로 만들도록 안내만 한다
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.Log("[Stage2Walk] 메뉴 EXOD ▸ Stage 2 산책 ▸ 테스트 씬 생성 으로 테스트 씬을 만들 수 있습니다.");
                    return;
                }
            }

            if (missing) BuildScenes(false);
            else RebuildWalkSceneOnly();
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.delayCall += TryAutoBuild;
        }

        static void RebuildWalkSceneOnly()
        {
            EnsureFolder(SceneFolder);
            EnsureFolder(Root + "/Placeholder");
            BuildWalkScene(GetPlaceholderSprite(), LoadUnlitMaterial());
            AddToBuildSettings(WalkScenePath, 1);
            EditorPrefs.SetInt(VersionKey, SceneVersion);
            EditorSceneManager.OpenScene(HomeScenePath);
            Debug.Log("[Stage2Walk] 산책 씬(Stage2_Walk)을 새 배경으로 다시 만들었습니다.");
        }

        [MenuItem("EXOD/Stage 2 산책/테스트 씬 생성")]
        public static void BuildFromMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("산책 테스트 씬", "플레이 모드를 끈 다음 실행하세요.", "확인");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if ((File.Exists(HomeScenePath) || File.Exists(WalkScenePath)) &&
                !EditorUtility.DisplayDialog("산책 테스트 씬", "이미 있는 Stage2_Home / Stage2_Walk 씬을 새로 만들어 덮어쓸까요?", "덮어쓰기", "취소"))
                return;
            BuildScenes(true);
        }

        [MenuItem("EXOD/Stage 2 산책/집 씬 열기")]
        public static void OpenHome()
        {
            if (!File.Exists(HomeScenePath)) { BuildFromMenu(); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(HomeScenePath);
        }

        static void BuildScenes(bool showDialog)
        {
            EnsureFolder(SceneFolder);
            EnsureFolder(Root + "/Placeholder");
            var sprite = GetPlaceholderSprite();
            var unlit = LoadUnlitMaterial();

            BuildWalkScene(sprite, unlit);
            BuildHomeScene(sprite, unlit);

            AddToBuildSettings(HomeScenePath, 0);
            AddToBuildSettings(WalkScenePath, 1);
            EditorPrefs.SetInt(VersionKey, SceneVersion);

            EditorSceneManager.OpenScene(HomeScenePath);
            Debug.Log("[Stage2Walk] 테스트 씬 생성 완료: Stage2_Home(시작) / Stage2_Walk");
            if (showDialog)
                EditorUtility.DisplayDialog("산책 테스트 씬", "Stage2_Home 씬을 열었습니다. Play를 눌러 현관문을 클릭해 보세요.", "확인");
        }

        // ---------------- 집 ----------------
        static void BuildHomeScene(Sprite sprite, Material unlit)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = MakeCamera(new Color(0.16f, 0.12f, 0.13f));

            new GameObject("DayClock").AddComponent<DayClock>();

            MakeBox("Floor", sprite, unlit, new Vector3(0f, -3.6f), new Vector3(20f, 1.2f), new Color(0.35f, 0.27f, 0.22f), -10, null);
            MakeBox("Wall", sprite, unlit, new Vector3(0f, 0.6f), new Vector3(20f, 7.2f), new Color(0.55f, 0.5f, 0.45f), -20, null);

            var door = MakeBox("FrontDoor", sprite, unlit, new Vector3(5.5f, -1.2f), new Vector3(1.8f, 3.6f), new Color(0.45f, 0.28f, 0.15f), 0, "현관문 (클릭: 산책)");
            door.AddComponent<BoxCollider2D>();
            var starter = door.AddComponent<FrontDoorWalkStarter>();
            SetRef(starter, "worldCamera", cam);

            var bed = MakeBox("Bed", sprite, unlit, new Vector3(-5f, -2.4f), new Vector3(3.2f, 1.2f), new Color(0.7f, 0.75f, 0.9f), 0, "침대 (클릭: 다음 날)");
            bed.AddComponent<BoxCollider2D>();
            var bedComp = bed.AddComponent<BedNextDay>();
            SetRef(bedComp, "worldCamera", cam);

            EditorSceneManager.SaveScene(scene, HomeScenePath);
        }

        // ---------------- 산책 ----------------
        static void BuildWalkScene(Sprite sprite, Material unlit)
        {
            var bgSprite = GetBackgroundSprite();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = MakeCamera(Color.black);

            // 배경: 5760x1080 → 가로 53.3 x 세로 10 유닛, 원점 중심
            SpriteRenderer bgRenderer = null;
            if (bgSprite != null)
            {
                var bg = new GameObject("Background_NightStreet");
                bgRenderer = bg.AddComponent<SpriteRenderer>();
                bgRenderer.sprite = bgSprite;
                bgRenderer.sortingOrder = -100;
                if (unlit != null) bgRenderer.sharedMaterial = unlit;
            }
            else
            {
                Debug.LogWarning($"[Stage2Walk] 배경 이미지({BackgroundPath})를 찾지 못해 임시 바닥으로 만듭니다.");
                bgRenderer = MakeBox("Ground", sprite, unlit, new Vector3(0f, -3.4f), new Vector3(53.3f, 1.6f), new Color(0.25f, 0.25f, 0.28f), -100, null)
                    .GetComponent<SpriteRenderer>();
            }

            // 왕복 구간: 집 대문 앞 ↔ 버스 정류장 앞
            const float feetY = -3.2f;
            var leftEdge = new GameObject("WalkLeftEdge (집 대문)").transform;
            leftEdge.position = new Vector3(-24f, feetY);
            var rightEdge = new GameObject("WalkRightEdge (버스 정류장)").transform;
            rightEdge.position = new Vector3(23.2f, feetY);

            // 캐릭터 (임시 도형: 발 위치가 feetY)
            var charSize = new Vector3(0.9f, 2.4f);
            float charY = feetY + charSize.y * 0.5f;

            var player = MakeBox("Player", sprite, unlit, new Vector3(-24f, charY), charSize, new Color(0.4f, 0.7f, 1f), 10, "플레이어");
            var mover = player.AddComponent<WalkPlayerMover>();
            SetRef(mover, "spriteToFlip", player.GetComponent<SpriteRenderer>());
            SetRef(mover, "leftEdge", leftEdge);
            SetRef(mover, "rightEdge", rightEdge);

            // 얀데레: 화면(= 배경) 가운데에 고정
            var yan = MakeBox("Yandere", sprite, unlit, new Vector3(0f, charY), charSize, Color.white, 9, "그녀");
            var yc = yan.AddComponent<WalkYandere>();

            // 배경 속 상호작용 물체 (투명 판정 영역, 마우스를 올리면 이름 표시)
            var interactables = new GameObject("Interactables").transform;
            MakeHotspot(interactables, "집 대문", new Vector3(-25.1f, 0f), new Vector2(2.3f, 2.6f));
            MakeHotspot(interactables, "가로등 1", new Vector3(-16.7f, 1.4f), new Vector2(0.7f, 5.6f));
            MakeHotspot(interactables, "가로등 2", new Vector3(-5.0f, 1.4f), new Vector2(0.7f, 5.6f));
            MakeHotspot(interactables, "가로등 3", new Vector3(6.6f, 1.4f), new Vector2(0.7f, 5.6f));
            MakeHotspot(interactables, "가로등 4", new Vector3(18.35f, 1.4f), new Vector2(0.7f, 5.6f));
            MakeHotspot(interactables, "벤치", new Vector3(-3.2f, -1.2f), new Vector2(2.2f, 1.35f));
            MakeHotspot(interactables, "맨홀 1", new Vector3(-18.9f, -2.23f), new Vector2(1.6f, 0.6f));
            MakeHotspot(interactables, "물웅덩이", new Vector3(2.9f, -2.08f), new Vector2(4.7f, 0.8f));
            MakeHotspot(interactables, "맨홀 2", new Vector3(15.33f, -2.37f), new Vector2(1.6f, 0.6f));
            MakeHotspot(interactables, "버스 정류장", new Vector3(24.1f, 0.6f), new Vector2(1.6f, 1.4f));

            // 고정 카메라: 배경 전체가 한 화면에 들어오게 (16:9 기준 orthographicSize 15)
            var fit = cam.gameObject.AddComponent<WalkCameraFit>();
            SetRef(fit, "fitTarget", bgRenderer);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.orthographicSize = 15f;

            var mgr = new GameObject("WalkGameManager").AddComponent<WalkGameManager>();
            SetRef(mgr, "player", mover);
            SetRef(mgr, "yandere", yc);
            SetRef(mgr, "worldCamera", cam);
            var mso = new SerializedObject(mgr);
            mso.FindProperty("anyClickCountsAsInteraction").boolValue = true;
            mso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, WalkScenePath);
        }

        // ---------------- 도우미 ----------------
        static Camera MakeCamera(Color bg)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            return cam;
        }

        static GameObject MakeBox(string name, Sprite sprite, Material mat, Vector3 pos, Vector3 size, Color color, int order, string label)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            if (mat != null) sr.sharedMaterial = mat;

            if (!string.IsNullOrEmpty(label))
            {
                var lbl = go.AddComponent<WorldLabel>();
                var so = new SerializedObject(lbl);
                so.FindProperty("text").stringValue = label;
                so.FindProperty("offset").vector3Value = new Vector3(0f, size.y * 0.5f + 0.35f, 0f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return go;
        }

        static void MakeInteractable(string name, Sprite sprite, Material mat, Vector3 pos, Vector3 size, Color color)
        {
            var go = MakeBox(name, sprite, mat, pos, size, color, 1, name);
            go.AddComponent<BoxCollider2D>();
            var wi = go.AddComponent<WalkInteractable>();
            var so = new SerializedObject(wi);
            so.FindProperty("displayName").stringValue = name;
            so.ApplyModifiedPropertiesWithoutUndo();
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

        static void MakeHotspot(Transform parent, string name, Vector3 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = size;

            var wi = go.AddComponent<WalkInteractable>();
            var so = new SerializedObject(wi);
            so.FindProperty("displayName").stringValue = name;
            so.ApplyModifiedPropertiesWithoutUndo();

            var lbl = go.AddComponent<WorldLabel>();
            var lso = new SerializedObject(lbl);
            lso.FindProperty("text").stringValue = name;
            lso.FindProperty("offset").vector3Value = new Vector3(0f, size.y * 0.5f + 0.3f, 0f);
            lso.FindProperty("onlyWhenHovered").boolValue = true;
            lso.ApplyModifiedPropertiesWithoutUndo();
        }

        static Sprite GetBackgroundSprite()
        {
            if (!File.Exists(BackgroundPath)) return null;
            AssetDatabase.ImportAsset(BackgroundPath);
            var importer = AssetImporter.GetAtPath(BackgroundPath) as TextureImporter;
            if (importer == null) return null;

            bool dirty = importer.textureType != TextureImporterType.Sprite ||
                         importer.spriteImportMode != SpriteImportMode.Single ||
                         !Mathf.Approximately(importer.spritePixelsPerUnit, BackgroundPPU) ||
                         importer.maxTextureSize < 8192 ||
                         importer.mipmapEnabled;
            if (dirty)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = BackgroundPPU;
                importer.maxTextureSize = 8192;   // 5760px이라 기본값 2048이면 흐려짐
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
        }

        static void SetRef(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null) { Debug.LogWarning($"[Stage2Walk] {target.GetType().Name}.{property} 를 찾을 수 없습니다."); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Sprite GetPlaceholderSprite()
        {
            if (!File.Exists(SpritePath))
            {
                var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
                var px = new Color32[16 * 16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply();
                File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(SpritePath);

                var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 16f;
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
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

        static void AddToBuildSettings(string path, int index)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == path);
            index = Mathf.Clamp(index, 0, list.Count);
            list.Insert(index, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
