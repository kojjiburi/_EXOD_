using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace LockpickPrototype
{
    /// <summary>
    /// Adapts keyboard and mouse input to the lock-picking rules and renders
    /// a resolution-independent prototype without requiring scene wiring.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LockpickGameController : MonoBehaviour
    {
        private const float ReferenceWidth = 1600f;
        private const float ReferenceHeight = 900f;
        private const float PinTop = 300f;
        private const float PinBottom = 635f;

        private static readonly Color Background = Hex("#130D10");
        private static readonly Color Wallpaper = Hex("#211418");
        private static readonly Color Panel = Hex("#241A1B");
        private static readonly Color PanelEdge = Hex("#4B3535");
        private static readonly Color Brass = Hex("#9B7B4F");
        private static readonly Color BrassLight = Hex("#D5BA81");
        private static readonly Color BrassDark = Hex("#4A3525");
        private static readonly Color Rose = Hex("#BE4962");
        private static readonly Color Success = Hex("#C7B58B");
        private static readonly Color Red = Hex("#A91E3C");
        private static readonly Color Text = Hex("#E8DFD6");
        private static readonly Color MutedText = Hex("#A28E89");
        private static readonly Color Paper = Hex("#D8C8B0");
        private static readonly Color Ink = Hex("#4B3033");

        [Header("Project Integration")]
        [SerializeField] private bool openOnStart;
        [SerializeField] private bool pauseWorldWhileOpen = true;
        [SerializeField] private bool showCursorWhileOpen = true;

        [Header("Audio")]
        [SerializeField, Range(0f, 1f)] private float effectsVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.42f;

        [Header("Signals")]
        [SerializeField] private UnityEvent onOpened = new UnityEvent();
        [SerializeField] private UnityEvent onCompleted = new UnityEvent();
        [SerializeField] private UnityEvent onFailed = new UnityEvent();
        [SerializeField] private UnityEvent onClosed = new UnityEvent();

        private LockpickGameModel model;
        [NonSerialized]
        private Texture2D pixel;
        [NonSerialized]
        private Font gameFont;
        [NonSerialized]
        private AudioSource effectsSource;
        [NonSerialized]
        private AudioSource ambienceSource;
        [NonSerialized]
        private AudioClip selectClip;
        [NonSerialized]
        private AudioClip pinSetClip;
        [NonSerialized]
        private AudioClip slipClip;
        [NonSerialized]
        private AudioClip unlockClip;
        [NonSerialized]
        private AudioClip failureClip;
        [NonSerialized]
        private AudioClip heartbeatClip;
        [NonSerialized]
        private AudioClip ambienceClip;
        private GUIStyle labelStyle;
        private GUIStyle shadowStyle;
        private bool mouseLifting;
        private int observedAttemptRevision;
        private int observedSelectedPin;
        private LockpickPhase observedPhase;
        private float feedbackTime;
        private float shakeTime;
        private float nextHeartbeatTime;
        private float previousTimeScale;
        private string feedback = string.Empty;

        private CursorLockMode previousCursorLockMode;
        private bool previousCursorVisible;
        private bool initialized;
        private bool isOpen;
        private bool globalStateCaptured;

        public bool IsOpen => isOpen;
        public LockpickPhase Phase => model != null ? model.Phase : LockpickPhase.Playing;
        public UnityEvent OpenedEvent => onOpened;
        public UnityEvent CompletedEvent => onCompleted;
        public UnityEvent FailedEvent => onFailed;
        public UnityEvent ClosedEvent => onClosed;

        public event Action Opened;
        public event Action Completed;
        public event Action Failed;
        public event Action Closed;

        private void Awake()
        {
            if (openOnStart)
            {
                Open();
            }
        }

        public void Open()
        {
            if (isOpen)
            {
                return;
            }

            InitializeIfNeeded();
            model.StartNewCampaign();
            ResetObservedState();
            CaptureGlobalState();
            isOpen = true;

            if (ambienceSource != null && !ambienceSource.isPlaying)
            {
                ambienceSource.Play();
            }

            onOpened?.Invoke();
            Opened?.Invoke();
        }

        public void Close()
        {
            if (!isOpen)
            {
                return;
            }

            isOpen = false;
            mouseLifting = false;
            ambienceSource?.Stop();
            RestoreGlobalState();
            onClosed?.Invoke();
            Closed?.Invoke();
        }

        public void Restart()
        {
            if (!isOpen || model == null)
            {
                return;
            }

            model.RestartStage();
            ResetObservedState();
            feedback = "처음부터 다시...";
            feedbackTime = 1.2f;
        }

        private void OnDisable()
        {
            if (!isOpen)
            {
                return;
            }

            isOpen = false;
            mouseLifting = false;
            ambienceSource?.Stop();
            RestoreGlobalState();
        }

        private void OnDestroy()
        {
            RestoreGlobalState();

            if (pixel != null)
            {
                Destroy(pixel);
            }

            if (gameFont != null)
            {
                Destroy(gameFont);
            }

            DestroyGeneratedClip(selectClip);
            DestroyGeneratedClip(pinSetClip);
            DestroyGeneratedClip(slipClip);
            DestroyGeneratedClip(unlockClip);
            DestroyGeneratedClip(failureClip);
            DestroyGeneratedClip(heartbeatClip);
            DestroyGeneratedClip(ambienceClip);
        }

        private void Update()
        {
            if (!isOpen || model == null)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (model.Phase == LockpickPhase.Playing)
            {
                HandleSelectionInput(keyboard);
                HandleMousePress(mouse);

                bool keyboardLift = keyboard != null &&
                    (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed);
                bool pointerLift = mouseLifting && mouse != null && mouse.leftButton.isPressed;
                model.Tick(Time.unscaledDeltaTime, keyboardLift || pointerLift);

                bool setPressed = keyboard != null &&
                    (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
                if (setPressed)
                {
                    model.TrySetSelectedPin();
                }

                if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                {
                    Restart();
                }
            }
            else
            {
                bool confirm = keyboard != null &&
                    (keyboard.spaceKey.wasPressedThisFrame ||
                     keyboard.enterKey.wasPressedThisFrame ||
                     keyboard.rKey.wasPressedThisFrame);

                if (confirm || (mouse != null && mouse.leftButton.wasPressedThisFrame))
                {
                    if (model.Phase == LockpickPhase.StageCleared)
                    {
                        Close();
                    }
                    else
                    {
                        Restart();
                    }
                }
            }

            ObserveAttempt();
            ObserveSelection();
            ObservePhase();
            UpdateHeartbeat();
            feedbackTime = Mathf.Max(0f, feedbackTime - Time.unscaledDeltaTime);
            shakeTime = Mathf.Max(0f, shakeTime - Time.unscaledDeltaTime);
        }

        private void HandleSelectionInput(Keyboard keyboard)
        {
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
            {
                model.MoveSelection(-1);
            }

            if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
            {
                model.MoveSelection(1);
            }
        }

        private void HandleMousePress(Mouse mouse)
        {
            if (mouse == null)
            {
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                int hoveredPin = PinAtScreenPosition(mouse.position.ReadValue());
                mouseLifting = hoveredPin >= 0;

                if (mouseLifting)
                {
                    model.SelectPin(hoveredPin);
                }
            }

            if (mouse.leftButton.wasReleasedThisFrame && mouseLifting)
            {
                model.TrySetSelectedPin();
                mouseLifting = false;
            }
        }

        private void InitializeIfNeeded()
        {
            if (initialized)
            {
                return;
            }

            model = new LockpickGameModel(Environment.TickCount);
            SetupAudio();
            initialized = true;
        }

        private void ResetObservedState()
        {
            observedAttemptRevision = model.AttemptRevision;
            observedSelectedPin = model.SelectedPin;
            observedPhase = model.Phase;
            feedback = string.Empty;
            feedbackTime = 0f;
            shakeTime = 0f;
            nextHeartbeatTime = Time.unscaledTime;
        }

        private void CaptureGlobalState()
        {
            if (globalStateCaptured)
            {
                return;
            }

            previousTimeScale = Time.timeScale;
            previousCursorLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            globalStateCaptured = true;

            if (pauseWorldWhileOpen)
            {
                Time.timeScale = 0f;
            }

            if (showCursorWhileOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void RestoreGlobalState()
        {
            if (!globalStateCaptured)
            {
                return;
            }

            if (pauseWorldWhileOpen)
            {
                Time.timeScale = previousTimeScale;
            }

            if (showCursorWhileOpen)
            {
                Cursor.lockState = previousCursorLockMode;
                Cursor.visible = previousCursorVisible;
            }

            globalStateCaptured = false;
        }

        private void ObserveAttempt()
        {
            if (observedAttemptRevision == model.AttemptRevision)
            {
                return;
            }

            observedAttemptRevision = model.AttemptRevision;

            switch (model.LastAttempt)
            {
                case LockpickAttempt.Success:
                    feedback = model.Phase == LockpickPhase.StageCleared ? "철컥..." : "딸깍";
                    feedbackTime = 1.1f;
                    PlaySound(model.Phase == LockpickPhase.StageCleared ? unlockClip : pinSetClip);
                    break;
                case LockpickAttempt.Miss:
                    feedback = "머리핀이 미끄러졌다";
                    feedbackTime = 1.1f;
                    shakeTime = 0.22f;
                    PlaySound(slipClip);
                    break;
            }
        }

        private void ObserveSelection()
        {
            if (observedSelectedPin == model.SelectedPin)
            {
                return;
            }

            observedSelectedPin = model.SelectedPin;
            PlaySound(selectClip, 0.55f);
        }

        private void ObservePhase()
        {
            if (observedPhase == model.Phase)
            {
                return;
            }

            observedPhase = model.Phase;
            nextHeartbeatTime = Time.unscaledTime;

            if (model.Phase == LockpickPhase.Failed)
            {
                PlaySound(failureClip);
                onFailed?.Invoke();
                Failed?.Invoke();
            }
            else if (model.Phase == LockpickPhase.StageCleared)
            {
                onCompleted?.Invoke();
                Completed?.Invoke();
            }
        }

        private void UpdateHeartbeat()
        {
            if (model.Phase != LockpickPhase.Playing || model.TimeRemaining > 10f)
            {
                return;
            }

            if (Time.unscaledTime < nextHeartbeatTime)
            {
                return;
            }

            float urgency = 1f - Mathf.Clamp01(model.TimeRemaining / 10f);
            PlaySound(heartbeatClip, Mathf.Lerp(0.4f, 0.75f, urgency));
            nextHeartbeatTime = Time.unscaledTime + Mathf.Lerp(0.95f, 0.48f, urgency);
        }

        private void SetupAudio()
        {
            effectsSource = gameObject.AddComponent<AudioSource>();
            effectsSource.playOnAwake = false;
            effectsSource.loop = false;
            effectsSource.spatialBlend = 0f;
            effectsSource.volume = effectsVolume;
            effectsSource.ignoreListenerPause = true;

            ambienceSource = gameObject.AddComponent<AudioSource>();
            ambienceSource.playOnAwake = false;
            ambienceSource.loop = true;
            ambienceSource.spatialBlend = 0f;
            ambienceSource.volume = ambienceVolume;
            ambienceSource.ignoreListenerPause = true;

            selectClip = CreateProceduralClip("Hairpin Select", 0.07f, time =>
                Mathf.Sin(Mathf.PI * 2f * (1850f - (time * 8000f)) * time) *
                Mathf.Exp(-42f * time) * 0.32f);

            pinSetClip = CreateProceduralClip("Pin Set", 0.18f, time =>
                (DecayPulse(time, 0f, 920f, 34f) * 0.48f) +
                (DecayPulse(time, 0.035f, 1420f, 42f) * 0.24f));

            slipClip = CreateProceduralClip("Hairpin Slip", 0.38f, time =>
                (Noise(time * 19500f) * Mathf.Exp(-7f * time) * 0.19f) +
                (DecayPulse(time, 0.025f, 310f, 10f) * 0.28f));

            unlockClip = CreateProceduralClip("Lock Opens", 0.9f, time =>
                (DecayPulse(time, 0f, 115f, 9f) * 0.65f) +
                (DecayPulse(time, 0.19f, 175f, 12f) * 0.52f) +
                (DecayPulse(time, 0.42f, 72f, 7f) * 0.42f));

            failureClip = CreateProceduralClip("Approaching Footsteps", 1.25f, time =>
                (DecayPulse(time, 0.02f, 58f, 12f) * 0.62f) +
                (DecayPulse(time, 0.39f, 54f, 11f) * 0.7f) +
                (DecayPulse(time, 0.82f, 49f, 9f) * 0.8f));

            heartbeatClip = CreateProceduralClip("Heartbeat", 0.44f, time =>
                (DecayPulse(time, 0.01f, 54f, 19f) * 0.62f) +
                (DecayPulse(time, 0.19f, 48f, 22f) * 0.48f));

            ambienceClip = CreateProceduralClip("Locked Room Ambience", 4f, time =>
            {
                float slowPulse = 0.65f + (Mathf.Sin(Mathf.PI * 0.5f * time) * 0.18f);
                float hum = Mathf.Sin(Mathf.PI * 2f * 37f * time) * 0.035f;
                float overtone = Mathf.Sin(Mathf.PI * 2f * 53f * time) * 0.018f;
                return (hum + overtone) * slowPulse;
            });

            ambienceSource.clip = ambienceClip;
        }

        private void PlaySound(AudioClip clip, float volumeScale = 1f)
        {
            if (effectsSource == null || clip == null)
            {
                return;
            }

            effectsSource.PlayOneShot(clip, volumeScale);
        }

        private static AudioClip CreateProceduralClip(string name, float duration, Func<float, float> sampleProvider)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * sampleRate));
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = Mathf.Clamp(sampleProvider(i / (float)sampleRate), -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.SetData(samples, 0);
            return clip;
        }

        private static float DecayPulse(float time, float start, float frequency, float decay)
        {
            if (time < start)
            {
                return 0f;
            }

            float elapsed = time - start;
            return Mathf.Sin(Mathf.PI * 2f * frequency * elapsed) * Mathf.Exp(-decay * elapsed);
        }

        private static float Noise(float value)
        {
            float raw = Mathf.Sin(value * 12.9898f) * 43758.5453f;
            return (Mathf.Repeat(raw, 1f) * 2f) - 1f;
        }

        private static void DestroyGeneratedClip(AudioClip clip)
        {
            if (clip != null)
            {
                Destroy(clip);
            }
        }

        private int PinAtScreenPosition(Vector2 screenPosition)
        {
            float scale = Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight);
            if (scale <= 0f)
            {
                return -1;
            }

            float offsetX = (Screen.width - (ReferenceWidth * scale)) * 0.5f;
            float offsetY = (Screen.height - (ReferenceHeight * scale)) * 0.5f;
            Vector2 referencePoint = new Vector2(
                (screenPosition.x - offsetX) / scale,
                ((Screen.height - screenPosition.y) - offsetY) / scale);

            for (int i = 0; i < model.PinCount; i++)
            {
                if (GetPinHitRect(i).Contains(referencePoint))
                {
                    return i;
                }
            }

            return -1;
        }

        private void OnGUI()
        {
            if (!isOpen || model == null)
            {
                return;
            }

            EnsureGuiResources();

            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            float scale = Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight);
            float offsetX = (Screen.width - (ReferenceWidth * scale)) * 0.5f;
            float offsetY = (Screen.height - (ReferenceHeight * scale)) * 0.5f;
            GUI.matrix = Matrix4x4.TRS(
                new Vector3(offsetX, offsetY, 0f),
                Quaternion.identity,
                new Vector3(scale, scale, 1f));

            DrawBackdrop();
            DrawHeader();
            DrawLock();
            DrawFooter();
            DrawOverlay();

            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        private void DrawBackdrop()
        {
            DrawRect(new Rect(0f, 0f, ReferenceWidth, ReferenceHeight), Background);

            for (int stripe = 0; stripe < 20; stripe++)
            {
                Color stripeColor = stripe % 2 == 0
                    ? Wallpaper
                    : new Color(Wallpaper.r * 0.72f, Wallpaper.g * 0.72f, Wallpaper.b * 0.72f, 1f);
                DrawRect(new Rect(stripe * 80f, 0f, 80f, ReferenceHeight), stripeColor);
                DrawRect(new Rect((stripe * 80f) + 5f, 0f, 2f, ReferenceHeight), new Color(0f, 0f, 0f, 0.15f));
            }

            DrawRect(new Rect(0f, 690f, ReferenceWidth, 210f), Hex("#0D090B"));
            DrawRect(new Rect(0f, 688f, ReferenceWidth, 5f), Hex("#382227"));

            DrawRect(new Rect(178f, 135f, 3f, 165f), new Color(0f, 0f, 0f, 0.2f));
            DrawRect(new Rect(181f, 292f, 58f, 3f), new Color(0f, 0f, 0f, 0.16f));
            DrawRect(new Rect(1390f, 82f, 3f, 210f), new Color(0f, 0f, 0f, 0.22f));

            for (int edge = 0; edge < 12; edge++)
            {
                float size = 18f + (edge * 12f);
                float alpha = 0.035f + (edge * 0.006f);
                Color shadow = new Color(0f, 0f, 0f, alpha);
                DrawRect(new Rect(0f, 0f, ReferenceWidth, size), shadow);
                DrawRect(new Rect(0f, ReferenceHeight - size, ReferenceWidth, size), shadow);
                DrawRect(new Rect(0f, 0f, size, ReferenceHeight), shadow);
                DrawRect(new Rect(ReferenceWidth - size, 0f, size, ReferenceHeight), shadow);
            }

            if (model.TimeRemaining < 10f && model.Phase == LockpickPhase.Playing)
            {
                float heartbeat = 0.12f + (Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4.8f)), 8f) * 0.18f);
                Color danger = new Color(Red.r, Red.g, Red.b, heartbeat);
                DrawRect(new Rect(0f, 0f, ReferenceWidth, 45f), danger);
                DrawRect(new Rect(0f, ReferenceHeight - 45f, ReferenceWidth, 45f), danger);
                DrawRect(new Rect(0f, 0f, 45f, ReferenceHeight), danger);
                DrawRect(new Rect(ReferenceWidth - 45f, 0f, 45f, ReferenceHeight), danger);
            }
        }

        private void DrawHeader()
        {
            Rect note = new Rect(82f, 48f, 420f, 112f);
            DrawRect(new Rect(note.x + 8f, note.y + 9f, note.width, note.height), new Color(0f, 0f, 0f, 0.35f));
            DrawRect(note, Paper);
            DrawRect(new Rect(note.x, note.y, note.width, 7f), Hex("#B4A187"));
            DrawText(new Rect(note.x + 28f, note.y + 18f, note.width - 56f, 42f), "문은 절대 열지 마.", 25, FontStyle.Bold, Ink, TextAnchor.MiddleLeft);
            DrawText(new Rect(note.x + 30f, note.y + 62f, note.width - 60f, 28f), "약속했잖아?", 17, FontStyle.Italic, Hex("#7D2E3F"), TextAnchor.MiddleRight);

            DrawText(new Rect(545f, 55f, 500f, 38f), "마지막 잠금 장치", 18, FontStyle.Bold, MutedText, TextAnchor.MiddleCenter);
            DrawText(new Rect(545f, 88f, 500f, 46f), "소리를 내면 안 돼", 28, FontStyle.Normal, Text, TextAnchor.MiddleCenter);

            int time = Mathf.CeilToInt(model.TimeRemaining);
            Color timerColor = model.TimeRemaining < 10f ? Hex("#E34B64") : Text;
            DrawText(new Rect(1170f, 48f, 330f, 28f), "발소리가 돌아오기까지", 14, FontStyle.Normal, MutedText, TextAnchor.MiddleRight);
            DrawText(new Rect(1170f, 77f, 330f, 58f), $"{time / 60:00}:{time % 60:00}", 38, FontStyle.Bold, timerColor, TextAnchor.MiddleRight);
        }

        private void DrawLock()
        {
            float shake = shakeTime > 0f ? Mathf.Sin(Time.unscaledTime * 85f) * 8f : 0f;
            Rect doorPlate = new Rect(300f + shake, 190f, 1000f, 535f);

            DrawRect(new Rect(doorPlate.x - 18f, doorPlate.y - 18f, doorPlate.width + 36f, doorPlate.height + 36f), new Color(0f, 0f, 0f, 0.42f));
            DrawRect(doorPlate, BrassDark);
            DrawRect(new Rect(doorPlate.x + 9f, doorPlate.y + 9f, doorPlate.width - 18f, doorPlate.height - 18f), Hex("#332623"));
            DrawOutline(new Rect(doorPlate.x + 22f, doorPlate.y + 22f, doorPlate.width - 44f, doorPlate.height - 44f), 2f, Brass);

            DrawScrew(new Vector2(doorPlate.x + 39f, doorPlate.y + 39f));
            DrawScrew(new Vector2(doorPlate.xMax - 39f, doorPlate.y + 39f));
            DrawScrew(new Vector2(doorPlate.x + 39f, doorPlate.yMax - 39f));
            DrawScrew(new Vector2(doorPlate.xMax - 39f, doorPlate.yMax - 39f));

            DrawText(new Rect(doorPlate.x + 60f, doorPlate.y + 24f, doorPlate.width - 120f, 38f), "핀 끝을 붉은 선에 맞춰", 16, FontStyle.Normal, MutedText, TextAnchor.MiddleCenter);

            float chamberLeft = GetPinCenter(0) - 54f + shake;
            float chamberRight = GetPinCenter(model.PinCount - 1) + 54f + shake;
            float shearY = PinBottom - (LockpickGameModel.ShearHeight * (PinBottom - PinTop));
            float tolerancePixels = model.SetTolerance * (PinBottom - PinTop);

            DrawRect(new Rect(chamberLeft - 24f, shearY - tolerancePixels, chamberRight - chamberLeft + 48f, tolerancePixels * 2f), new Color(Rose.r, Rose.g, Rose.b, 0.08f));
            DrawRect(new Rect(chamberLeft - 24f, shearY - 1f, chamberRight - chamberLeft + 48f, 2f), new Color(Rose.r, Rose.g, Rose.b, 0.82f));
            DrawText(new Rect(chamberRight + 34f, shearY - 15f, 110f, 30f), "전단선", 12, FontStyle.Normal, Rose, TextAnchor.MiddleLeft);

            for (int i = 0; i < model.PinCount; i++)
            {
                DrawPin(i, shake);
            }

            DrawPickTool(shake);
            DrawDurability(doorPlate);
        }

        private void DrawPin(int index, float shake)
        {
            float centerX = GetPinCenter(index) + shake;
            bool isSelected = index == model.SelectedPin && model.Phase == LockpickPhase.Playing;
            bool isSet = model.IsPinSet(index);
            float trackHeight = PinBottom - PinTop;
            float pinHeight = model.GetPinHeight(index) * trackHeight;
            float pinY = PinBottom - pinHeight;

            DrawRect(new Rect(centerX - 48f, PinTop - 10f, 96f, trackHeight + 20f), Hex("#160F11"));
            DrawOutline(new Rect(centerX - 48f, PinTop - 10f, 96f, trackHeight + 20f), isSelected ? Rose : PanelEdge, isSelected ? 2f : 1f);

            for (int spring = 0; spring < 6; spring++)
            {
                float y = PinTop + 16f + (spring * 22f);
                DrawRect(new Rect(centerX - 19f + ((spring % 2) * 8f), y, 30f, 2f), isSet ? Success : BrassDark);
            }

            Color pinColor = isSet ? Success : (isSelected ? BrassLight : Brass);
            DrawRect(new Rect(centerX - 25f, pinY, 50f, pinHeight), pinColor);
            DrawRect(new Rect(centerX - 31f, pinY, 62f, 10f), isSet ? Hex("#E6D7AF") : BrassLight);
            DrawRect(new Rect(centerX - 25f, PinBottom - 12f, 50f, 12f), BrassDark);

            if (isSelected)
            {
                float pulse = 0.5f + (Mathf.Sin(Time.unscaledTime * 5f) * 0.25f);
                DrawRect(new Rect(centerX - 40f, PinBottom + 22f, 80f, 3f), new Color(Rose.r, Rose.g, Rose.b, pulse));
                DrawText(new Rect(centerX - 50f, PinBottom + 30f, 100f, 28f), "선택", 11, FontStyle.Normal, Rose, TextAnchor.MiddleCenter);
            }
            else if (isSet)
            {
                DrawText(new Rect(centerX - 45f, PinBottom + 28f, 90f, 28f), "고정됨", 11, FontStyle.Normal, Success, TextAnchor.MiddleCenter);
            }

            DrawText(new Rect(centerX - 35f, PinTop - 43f, 70f, 26f), (index + 1).ToString(), 12, FontStyle.Normal, MutedText, TextAnchor.MiddleCenter);
        }

        private void DrawPickTool(float shake)
        {
            float selectedX = GetPinCenter(model.SelectedPin) + shake;
            float pinY = PinBottom - (model.GetPinHeight(model.SelectedPin) * (PinBottom - PinTop));

            DrawRect(new Rect(350f + shake, 675f, 175f, 13f), Hex("#6A5D55"));
            DrawRect(new Rect(365f + shake, 678f, Mathf.Max(0f, selectedX - 380f), 4f), Hex("#C7B9A5"));
            DrawRect(new Rect(selectedX - 3f, Mathf.Min(pinY + 20f, 670f), 6f, Mathf.Max(8f, 670f - pinY - 20f)), Hex("#C7B9A5"));
        }

        private void DrawDurability(Rect doorPlate)
        {
            DrawText(new Rect(doorPlate.x + 55f, doorPlate.y + doorPlate.height - 64f, 160f, 28f), "남은 머리핀", 12, FontStyle.Normal, MutedText, TextAnchor.MiddleLeft);

            for (int i = 0; i < 5; i++)
            {
                Color color = i < model.PicksRemaining ? Hex("#C7B9A5") : Hex("#45363A");
                float x = doorPlate.x + 190f + (i * 39f);
                float y = doorPlate.y + doorPlate.height - 52f;
                DrawRect(new Rect(x, y, 26f, 3f), color);
                DrawRect(new Rect(x + 20f, y - 5f, 3f, 8f), color);
            }

            DrawText(new Rect(doorPlate.x + doorPlate.width - 330f, doorPlate.y + doorPlate.height - 65f, 275f, 30f), "너무 세게 밀면 부러진다", 12, FontStyle.Italic, Hex("#7D6062"), TextAnchor.MiddleRight);
        }

        private void DrawFooter()
        {
            Rect controls = new Rect(390f, 760f, 820f, 82f);
            DrawRect(new Rect(controls.x + 7f, controls.y + 8f, controls.width, controls.height), new Color(0f, 0f, 0f, 0.3f));
            DrawRect(controls, new Color(Paper.r, Paper.g, Paper.b, 0.9f));
            DrawText(new Rect(controls.x + 24f, controls.y + 11f, controls.width - 48f, 27f),
                "A / D 핀 선택     W / ↑ 들어 올리기     SPACE 고정     ESC 닫기",
                14, FontStyle.Bold, Ink, TextAnchor.MiddleCenter);
            DrawRect(new Rect(controls.x + 54f, controls.y + 43f, controls.width - 108f, 1f), new Color(Ink.r, Ink.g, Ink.b, 0.22f));
            DrawText(new Rect(controls.x + 24f, controls.y + 48f, controls.width - 48f, 23f),
                "마우스: 핀을 누른 채 올리고, 붉은 선에서 놓기",
                13, FontStyle.Normal, Hex("#6B4A4D"), TextAnchor.MiddleCenter);

            if (feedbackTime > 0f)
            {
                Color feedbackColor = model.LastAttempt == LockpickAttempt.Miss ? Hex("#E34B64") : Success;
                DrawText(new Rect(560f, 148f, 480f, 46f), feedback, 23, FontStyle.Bold, feedbackColor, TextAnchor.MiddleCenter);
            }
        }

        private void DrawOverlay()
        {
            if (model.Phase == LockpickPhase.Playing)
            {
                return;
            }

            bool failed = model.Phase == LockpickPhase.Failed;
            DrawRect(new Rect(0f, 0f, ReferenceWidth, ReferenceHeight), new Color(0.035f, 0.01f, 0.018f, 0.9f));
            Rect card = new Rect(470f, 235f, 660f, 410f);
            DrawRect(new Rect(card.x + 12f, card.y + 14f, card.width, card.height), new Color(0f, 0f, 0f, 0.5f));
            DrawRect(card, failed ? Hex("#241014") : Hex("#211B18"));
            DrawOutline(card, 2f, failed ? Red : Brass);

            string title = failed
                ? "어디 가려고?"
                : "문이 열렸다";
            string subtitle = failed
                ? "문밖의 발소리가 멈췄다."
                : "아직 들키지 않았다. 지금은.";
            string action = failed
                ? "ENTER 또는 클릭 · 다시 시도"
                : "ENTER 또는 클릭 · 돌아가기";
            Color accent = failed ? Hex("#E34B64") : Success;

            DrawText(new Rect(card.x + 45f, card.y + 63f, card.width - 90f, 76f), title, 39, FontStyle.Bold, accent, TextAnchor.MiddleCenter);
            DrawText(new Rect(card.x + 45f, card.y + 151f, card.width - 90f, 42f), subtitle, 18, FontStyle.Normal, Text, TextAnchor.MiddleCenter);
            DrawRect(new Rect(card.x + 110f, card.y + 245f, card.width - 220f, 1f), PanelEdge);
            DrawText(new Rect(card.x + 45f, card.y + 282f, card.width - 90f, 38f), action, 14, FontStyle.Bold, MutedText, TextAnchor.MiddleCenter);
            DrawText(new Rect(card.x + 45f, card.y + 335f, card.width - 90f, 28f), failed ? "사랑하면 도망가면 안 되잖아." : "숨을 죽이고 움직여.", 13, FontStyle.Italic, failed ? Rose : Hex("#756561"), TextAnchor.MiddleCenter);
        }

        private void DrawScrew(Vector2 center)
        {
            DrawRect(new Rect(center.x - 8f, center.y - 8f, 16f, 16f), Brass);
            DrawRect(new Rect(center.x - 6f, center.y - 1f, 12f, 2f), BrassDark);
        }

        private Rect GetPinHitRect(int index)
        {
            float centerX = GetPinCenter(index);
            return new Rect(centerX - 55f, PinTop - 20f, 110f, PinBottom - PinTop + 90f);
        }

        private float GetPinCenter(int index)
        {
            float spacing = Mathf.Min(130f, 620f / Mathf.Max(1, model.PinCount - 1));
            float totalWidth = spacing * (model.PinCount - 1);
            return (ReferenceWidth * 0.5f) - (totalWidth * 0.5f) + (index * spacing);
        }

        private void EnsureGuiResources()
        {
            if (pixel == null)
            {
                pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    name = "Lockpick UI Pixel",
                    hideFlags = HideFlags.HideAndDontSave
                };
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
            }

            if (labelStyle == null)
            {
                gameFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" },
                    24);
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    clipping = TextClipping.Clip,
                    wordWrap = false,
                    font = gameFont != null ? gameFont : GUI.skin.label.font
                };
                shadowStyle = new GUIStyle(labelStyle);
            }
        }

        private void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, pixel);
            GUI.color = previous;
        }

        private void DrawOutline(Rect rect, float thickness, Color color)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private void DrawOutline(Rect rect, Color color, float thickness)
        {
            DrawOutline(rect, thickness, color);
        }

        private void DrawText(Rect rect, string value, int size, FontStyle style, Color color, TextAnchor alignment)
        {
            labelStyle.fontSize = size;
            labelStyle.fontStyle = style;
            labelStyle.alignment = alignment;
            labelStyle.normal.textColor = color;

            shadowStyle.fontSize = size;
            shadowStyle.fontStyle = style;
            shadowStyle.alignment = alignment;
            shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.55f);

            Rect shadowRect = new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height);
            GUI.Label(shadowRect, value, shadowStyle);
            GUI.Label(rect, value, labelStyle);
        }

        private static Color Hex(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out Color color) ? color : Color.white;
        }
    }
}
