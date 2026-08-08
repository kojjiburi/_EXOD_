using System;

namespace LockpickPrototype
{
    public enum LockpickPhase
    {
        Playing,
        StageCleared,
        Failed
    }

    public enum LockpickAttempt
    {
        None,
        Success,
        Miss
    }

    /// <summary>
    /// Owns the deterministic rules for one lock-picking session.
    /// Presentation and raw device input stay in LockpickGameController.
    /// </summary>
    public sealed class LockpickGameModel
    {
        public const int FinalStage = 1;
        public const float ShearHeight = 0.72f;

        private readonly Random random;
        private float[] pinHeights = Array.Empty<float>();
        private bool[] setPins = Array.Empty<bool>();
        private int scoreAtStageStart;

        public LockpickGameModel(int seed)
        {
            random = new Random(seed);
            StartNewCampaign();
        }

        public LockpickPhase Phase { get; private set; }
        public LockpickAttempt LastAttempt { get; private set; }
        public int AttemptRevision { get; private set; }
        public int Stage { get; private set; }
        public int SelectedPin { get; private set; }
        public int PicksRemaining { get; private set; }
        public int Score { get; private set; }
        public float TimeRemaining { get; private set; }
        public float StageTimeLimit { get; private set; }
        public float SetTolerance { get; private set; }
        public int PinCount => pinHeights.Length;
        public bool CampaignComplete => Phase == LockpickPhase.StageCleared && Stage == FinalStage;

        public float GetPinHeight(int index)
        {
            return pinHeights[index];
        }

        public bool IsPinSet(int index)
        {
            return setPins[index];
        }

        public void StartNewCampaign()
        {
            Score = 0;
            Stage = 1;
            scoreAtStageStart = 0;
            BeginStage();
        }

        public void RestartStage()
        {
            Score = scoreAtStageStart;
            BeginStage();
        }

        public void ContinueCampaign()
        {
            if (Phase != LockpickPhase.StageCleared)
            {
                return;
            }

            if (Stage >= FinalStage)
            {
                StartNewCampaign();
                return;
            }

            Stage++;
            scoreAtStageStart = Score;
            BeginStage();
        }

        public void Tick(float deltaTime, bool liftSelectedPin)
        {
            if (Phase != LockpickPhase.Playing)
            {
                return;
            }

            TimeRemaining -= Math.Max(0f, deltaTime);
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                Phase = LockpickPhase.Failed;
                return;
            }

            if (setPins[SelectedPin])
            {
                return;
            }

            float liftSpeed = 0.53f + (Stage * 0.08f);
            float fallSpeed = 0.16f + (Stage * 0.05f);
            float direction = liftSelectedPin ? liftSpeed : -fallSpeed;
            pinHeights[SelectedPin] = Clamp01(pinHeights[SelectedPin] + (direction * deltaTime));
        }

        public void MoveSelection(int direction)
        {
            if (Phase != LockpickPhase.Playing || direction == 0)
            {
                return;
            }

            int step = direction > 0 ? 1 : -1;
            int candidate = SelectedPin;

            for (int i = 0; i < PinCount; i++)
            {
                candidate = (candidate + step + PinCount) % PinCount;
                if (!setPins[candidate])
                {
                    SelectedPin = candidate;
                    return;
                }
            }
        }

        public void SelectPin(int index)
        {
            if (Phase != LockpickPhase.Playing || index < 0 || index >= PinCount || setPins[index])
            {
                return;
            }

            SelectedPin = index;
        }

        public bool TrySetSelectedPin()
        {
            if (Phase != LockpickPhase.Playing || setPins[SelectedPin])
            {
                return false;
            }

            float distance = Math.Abs(pinHeights[SelectedPin] - ShearHeight);
            AttemptRevision++;

            if (distance <= SetTolerance)
            {
                setPins[SelectedPin] = true;
                pinHeights[SelectedPin] = ShearHeight;
                LastAttempt = LockpickAttempt.Success;
                Score += 100 + (int)Math.Ceiling(TimeRemaining);

                if (AllPinsSet())
                {
                    Score += PicksRemaining * 75;
                    Phase = LockpickPhase.StageCleared;
                }
                else
                {
                    SelectNextUnsetPin();
                }

                return true;
            }

            LastAttempt = LockpickAttempt.Miss;
            PicksRemaining--;
            pinHeights[SelectedPin] = 0.08f;

            if (PicksRemaining <= 0)
            {
                PicksRemaining = 0;
                Phase = LockpickPhase.Failed;
            }

            return false;
        }

        private void BeginStage()
        {
            int pinCount = 3 + Stage;
            pinHeights = new float[pinCount];
            setPins = new bool[pinCount];

            for (int i = 0; i < pinCount; i++)
            {
                pinHeights[i] = 0.06f + ((float)random.NextDouble() * 0.18f);
            }

            SelectedPin = 0;
            PicksRemaining = Stage == 1 ? 5 : 4;
            StageTimeLimit = 50f - (Stage * 5f);
            TimeRemaining = StageTimeLimit;
            SetTolerance = 0.09f - (Stage * 0.015f);
            Phase = LockpickPhase.Playing;
            LastAttempt = LockpickAttempt.None;
            AttemptRevision++;
        }

        private void SelectNextUnsetPin()
        {
            for (int offset = 1; offset <= PinCount; offset++)
            {
                int candidate = (SelectedPin + offset) % PinCount;
                if (!setPins[candidate])
                {
                    SelectedPin = candidate;
                    return;
                }
            }
        }

        private bool AllPinsSet()
        {
            for (int i = 0; i < setPins.Length; i++)
            {
                if (!setPins[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }
}
