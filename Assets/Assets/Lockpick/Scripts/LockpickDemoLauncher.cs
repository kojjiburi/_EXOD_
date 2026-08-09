using UnityEngine;

namespace LockpickPrototype
{
    /// <summary>
    /// Opens the minigame only in a dedicated demo scene.
    /// Production scenes should call LockpickGameController.Open from their interaction flow.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LockpickGameController))]
    public sealed class LockpickDemoLauncher : MonoBehaviour
    {
        [SerializeField] private bool launchOnStart = true;

        private LockpickGameController minigame;

        private void Awake()
        {
            minigame = GetComponent<LockpickGameController>();
        }

        private void Start()
        {
            if (launchOnStart)
            {
                minigame.Open();
            }
        }
    }
}
