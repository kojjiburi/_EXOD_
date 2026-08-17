using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public sealed class QuickSlotPersistentRoot : MonoBehaviour
{
    private const string PrefabResourcePath = "QuickSlotSystem";
    private const string ExcludedSceneName = "StartScenes";

    public static QuickSlotPersistentRoot Instance { get; private set; }

    private EventSystem ownedEventSystem;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        if (scene.name == ExcludedSceneName || scene.name == "Intro")
        {
            if (Instance != null)
            {
                Instance.gameObject.SetActive(false);
                Destroy(Instance.gameObject);
            }

            return;
        }

        EnsureInstance();
        Instance?.EnsureEventSystem();
    }

    private static void EnsureInstance()
    {
        if (Instance != null)
            return;

        GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogError(
                "[QuickSlot] Resources/QuickSlotSystem.prefab을 찾지 못했습니다. " +
                "Tools/EXOD/Install Persistent Quick Slot을 다시 실행하세요.");
            return;
        }

        Instantiate(prefab);
    }

    private void EnsureEventSystem()
    {
        EventSystem[] systems = FindObjectsByType<EventSystem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        EventSystem sceneEventSystem = systems.FirstOrDefault(system => system != ownedEventSystem);
        if (sceneEventSystem != null)
        {
            if (ownedEventSystem != null)
                Destroy(ownedEventSystem.gameObject);

            ownedEventSystem = null;
            return;
        }

        if (ownedEventSystem != null)
            return;

        GameObject eventSystemObject = new GameObject(
            "QuickSlotEventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule));
        eventSystemObject.transform.SetParent(transform, false);
        ownedEventSystem = eventSystemObject.GetComponent<EventSystem>();
    }
}
