using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR;

namespace CetemVR
{
    /// <summary>
    /// Máquina de estados principal da experiência "Cápsula das Terras Raras - CETEM VR".
    /// Fluxo:
    /// 1. Ao colocar o óculos (IdleWaitingUser), aguarda 5 segundos (headsetAdjustmentDelay = 5s)
    ///    para a pessoa acomodar o óculos no rosto e inicia automaticamente.
    /// 2. Apresenta a boas-vindas do Mascote e os 4 minerais em sequência com hologramas 3D.
    /// 3. Ao final, exibe o quadro "Parabéns, explorador!", fala a mensagem completa até o fim
    ///    e TRAVA (CompletedLocked) na tela de Parabéns.
    /// 4. Só reinicia quando a pessoa tirar o óculos do rosto, a próxima colocar e aguardar 5 segundos.
    /// </summary>
    public class ExperienceManager : MonoBehaviour
    {
        public enum ExperienceState
        {
            IdleWaitingUser,
            IntroMascot,
            InteractiveExploration,
            OutroReset,
            CompletedLocked
        }

        [Header("Estado Atual (Somente Leitura)")]
        [SerializeField] private ExperienceState currentState = ExperienceState.IdleWaitingUser;

        [Header("Câmera VR e Gaze Raycast no Mascote")]
        [SerializeField] private Transform vrCameraTransform;
        [SerializeField] private float gazeMaxDistance = 7.0f;
        [SerializeField] private LayerMask interactableLayerMask = ~0;
        [SerializeField] private Image globalReticleFillImage;

        [Header("Estações dos 4 Minerais na Bancada")]
        [SerializeField] private List<GazeInteractableMineral> benchMinerals = new List<GazeInteractableMineral>();

        [Header("Mascote CETEM e Áudio")]
        [SerializeField] private AudioSource mascotAudioSource;
        [SerializeField] private AudioSource sfxAudioSource;
        [SerializeField] private AudioClip introWelcomeClip;
        [SerializeField] private float fallbackIntroDuration = 8.0f;
        [SerializeField] private float fallbackMineralAudioDuration = 12.0f;
        [SerializeField] private AudioClip outroConclusionClip;
        [SerializeField] private Animator mascotAnimator;
        [SerializeField] private CetemMascotController mascotController;
        [SerializeField] private ExperienceAnchor experienceAnchor;

        [Header("Tempos de Acomodação (5s)")]
        [Tooltip("Tempo (em segundos) após colocar o óculos para acomodar no rosto antes de iniciar automaticamente.")]
        [SerializeField] private float headsetAdjustmentDelay = 5.0f;

        [Header("Painéis Opcionais (Desativados para evitar sobreposição)")]
        [SerializeField] private GameObject idleWelcomePanel;
        [SerializeField] private GameObject outroConclusionPanel;

        [Header("Eventos de Ciclo de Vida")]
        public UnityEvent onExperienceStarted;
        public UnityEvent onExplorationPhaseStarted;
        public UnityEvent onExperienceCompleted;
        public UnityEvent onExperienceReset;

        private CetemMascotController _currentGazedMascot;
        private GazeInteractableMineral _activePresentingMineral;
        private float _adjustmentRemaining;
        private int _lastDisplayedCountdownSec = -1;
        private Coroutine _activeStateCoroutine;

        // Monitoramento de retirada e recolocação do óculos
        private bool _userPresenceEverTrue;
        private bool _headsetRemovedBySensor;
        private float _sensorAbsentTimer;

        // Monitoramento físico de retirada/troca do óculos quando travado no final (CompletedLocked)
        private Vector3 _lockedHeadPos;
        private bool _headsetMovedOffDuringLock;
        private float _offHeadTimer;
        private float _steadyOnHeadTimer;
        private Vector3 _lastFrameHeadPos;

        private static readonly int AnimIsTalking = Animator.StringToHash("IsTalking");
        private static readonly int AnimTriggerWave = Animator.StringToHash("Wave");

        public ExperienceState CurrentState => currentState;

        private void Awake()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            headsetAdjustmentDelay = 5.0f;

            if (vrCameraTransform == null && Camera.main != null)
            {
                vrCameraTransform = Camera.main.transform;
            }

            if (mascotController != null)
            {
                mascotController.MascotClicked += HandleMascotClicked;
            }
        }

        private void Start()
        {
            ResetToIdleState();
        }

        private void OnDestroy()
        {
            if (mascotController != null)
            {
                mascotController.MascotClicked -= HandleMascotClicked;
            }
        }

        /// <summary>
        /// Quando o visitante retira o Meta Quest 2 do rosto e o sistema pausa, para tudo;
        /// quando o próximo visitante coloca o óculos (pauseStatus == false), inicia a contagem de 5s.
        /// </summary>
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                StopAndHoldForHeadsetOff();
            }
            else
            {
                if (experienceAnchor != null) experienceAnchor.Recenter();
                ResetToIdleState();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                StopAndHoldForHeadsetOff();
            }
            else
            {
                if (experienceAnchor != null) experienceAnchor.Recenter();
                ResetToIdleState();
            }
        }

        private void Update()
        {
            MonitorHeadsetSensorAndExchange();

            if (currentState == ExperienceState.IdleWaitingUser)
            {
                UpdateIdleAdjustmentCountdown();
            }

            PerformMascotGazeRaycast();
        }

        /// <summary>
        /// Contagem regressiva de 5 segundos para a pessoa acomodar o óculos no rosto e iniciar automaticamente.
        /// </summary>
        private void UpdateIdleAdjustmentCountdown()
        {
            if (_headsetRemovedBySensor)
            {
                return;
            }

            if (_adjustmentRemaining > 0f)
            {
                _adjustmentRemaining -= Time.deltaTime;
                int sec = Mathf.CeilToInt(Mathf.Max(0f, _adjustmentRemaining));

                if (sec != _lastDisplayedCountdownSec)
                {
                    _lastDisplayedCountdownSec = sec;
                    if (mascotController != null)
                    {
                        mascotController.SetMascotPrompt(false, string.Empty);
                        mascotController.ShowStaticMessage(
                            "CÁPSULA DAS TERRAS RARAS - CETEM",
                            $"Acomode os óculos no rosto com conforto!\n\nIniciando a apresentação em {sec} segundos...");
                    }
                }

                if (_adjustmentRemaining <= 0f)
                {
                    if (experienceAnchor != null) experienceAnchor.Recenter();
                    StartExperience();
                }
            }
        }

        /// <summary>
        /// Monitora se a pessoa tirou o óculos e colocou novamente:
        /// 1. Pelo sensor de proximidade (CommonUsages.userPresence), se ativo no OpenXR.
        /// 2. No estado travado final (CompletedLocked), também detecta o movimento físico de tirar
        ///    o óculos da cabeça e acomodá-lo no próximo visitante.
        /// </summary>
        private void MonitorHeadsetSensorAndExchange()
        {
            InputDevice headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (headDevice.isValid && headDevice.TryGetFeatureValue(CommonUsages.userPresence, out bool userPresent))
            {
                if (userPresent)
                {
                    _userPresenceEverTrue = true;
                    _sensorAbsentTimer = 0f;

                    if (_headsetRemovedBySensor)
                    {
                        _headsetRemovedBySensor = false;
                        if (experienceAnchor != null) experienceAnchor.Recenter();
                        ResetToIdleState();
                        return;
                    }
                }
                else if (_userPresenceEverTrue)
                {
                    _sensorAbsentTimer += Time.deltaTime;
                    if (_sensorAbsentTimer >= 0.6f && !_headsetRemovedBySensor)
                    {
                        _headsetRemovedBySensor = true;
                        StopAndHoldForHeadsetOff();
                        return;
                    }
                }
            }

            // Se está travado na tela final de "Parabéns", detecta troca física do óculos (mesmo sem sleep do SO)
            if (currentState == ExperienceState.CompletedLocked && vrCameraTransform != null)
            {
                Vector3 pos = vrCameraTransform.position;
                Vector3 euler = vrCameraTransform.eulerAngles;
                float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
                float heightDelta = Mathf.Abs(pos.y - _lockedHeadPos.y);
                float horizontalDelta = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(_lockedHeadPos.x, _lockedHeadPos.z));

                // Ao tirar o óculos da cabeça, ele inclina bastante (> 28°) ou muda de altura/posição (> 18 cm)
                bool isBeingRemovedOrMoved = Mathf.Abs(pitch) > 28f || heightDelta > 0.18f || horizontalDelta > 0.30f;

                if (!_headsetMovedOffDuringLock)
                {
                    if (isBeingRemovedOrMoved)
                    {
                        _offHeadTimer += Time.deltaTime;
                        if (_offHeadTimer >= 0.45f)
                        {
                            _headsetMovedOffDuringLock = true;
                            _steadyOnHeadTimer = 0f;
                        }
                    }
                    else
                    {
                        _offHeadTimer = 0f;
                    }
                }
                else
                {
                    // Quando o próximo visitante coloca o óculos no rosto, a cabeça nivela (pitch < 20°) e estabiliza
                    float frameMoveSpeed = Vector3.Distance(pos, _lastFrameHeadPos) / Mathf.Max(0.001f, Time.deltaTime);
                    bool levelAndSteady = Mathf.Abs(pitch) < 20f && frameMoveSpeed < 0.35f;

                    if (levelAndSteady)
                    {
                        _steadyOnHeadTimer += Time.deltaTime;
                        if (_steadyOnHeadTimer >= 0.6f)
                        {
                            _headsetMovedOffDuringLock = false;
                            if (experienceAnchor != null) experienceAnchor.Recenter();
                            ResetToIdleState();
                        }
                    }
                    else
                    {
                        _steadyOnHeadTimer = 0f;
                    }
                }

                _lastFrameHeadPos = pos;
            }
        }

        private void StopAndHoldForHeadsetOff()
        {
            StopActiveStateCoroutine();
            _activePresentingMineral = null;

            if (mascotAudioSource != null) mascotAudioSource.Stop();
            if (mascotController != null)
            {
                mascotController.StopSpeaking();
                mascotController.SetMascotPrompt(false, string.Empty);
            }

            SetMascotTalkingAnimation(false);

            foreach (GazeInteractableMineral mineral in benchMinerals)
            {
                if (mineral != null)
                {
                    mineral.ResetMineralState();
                    mineral.SetInteractable(false);
                }
            }
        }

        private void PerformMascotGazeRaycast()
        {
            if (vrCameraTransform == null)
            {
                if (Camera.main == null) return;
                vrCameraTransform = Camera.main.transform;
            }

            Physics.SyncTransforms();

            Ray gazeRay = new Ray(vrCameraTransform.position, vrCameraTransform.forward);
            CetemMascotController hitMascot = null;

            if (Physics.Raycast(gazeRay, out RaycastHit hitInfo, gazeMaxDistance, interactableLayerMask, QueryTriggerInteraction.Collide))
            {
                var candidate = hitInfo.collider.GetComponentInParent<CetemMascotController>();
                if (candidate != null && candidate.IsInteractable)
                {
                    hitMascot = candidate;
                }
            }

            if (hitMascot != _currentGazedMascot)
            {
                if (_currentGazedMascot != null)
                {
                    _currentGazedMascot.OnGazeExit();
                }

                _currentGazedMascot = hitMascot;

                if (_currentGazedMascot != null)
                {
                    _currentGazedMascot.OnGazeEnter();
                }
            }

            if (globalReticleFillImage != null)
            {
                if (_currentGazedMascot != null && _currentGazedMascot.IsInteractable)
                {
                    globalReticleFillImage.fillAmount = _currentGazedMascot.NormalizedGazeProgress;
                }
                else
                {
                    globalReticleFillImage.fillAmount = 0f;
                }
            }
        }

        private void HandleMascotClicked()
        {
            if (currentState == ExperienceState.IdleWaitingUser)
            {
                StartExperience();
            }
        }

        [ContextMenu("Iniciar Experiência (StartExperience)")]
        public void StartExperience()
        {
            StopActiveStateCoroutine();
            _activeStateCoroutine = StartCoroutine(FullGuidedTourRoutine());
        }

        private IEnumerator FullGuidedTourRoutine()
        {
            currentState = ExperienceState.IntroMascot;

            if (_activePresentingMineral != null)
            {
                _activePresentingMineral.SetPresentationState(false);
                _activePresentingMineral = null;
            }

            foreach (GazeInteractableMineral mineral in benchMinerals)
            {
                if (mineral != null)
                {
                    mineral.ResetMineralState();
                    mineral.SetInteractable(false);
                }
            }

            SetUIState(idleActive: false, outroActive: false);

            if (mascotController != null)
            {
                mascotController.SetMascotPrompt(false, string.Empty);
            }

            onExperienceStarted?.Invoke();

            float waitDuration = 1.5f;
            if (mascotController != null)
            {
                waitDuration = mascotController.ShowDefaultWelcome();
            }
            else
            {
                waitDuration = PlayMascotVoice(introWelcomeClip, fallbackIntroDuration, triggerWave: true);
            }

            yield return new WaitForSeconds(waitDuration);
            SetMascotTalkingAnimation(false);

            currentState = ExperienceState.InteractiveExploration;
            onExplorationPhaseStarted?.Invoke();

            yield return new WaitForSeconds(0.8f);

            // Apresenta cada um dos 4 minerais em sequência
            for (int i = 0; i < benchMinerals.Count; i++)
            {
                GazeInteractableMineral mineral = benchMinerals[i];
                if (mineral == null) continue;

                yield return PresentSingleMineralStep(mineral);
                yield return new WaitForSeconds(1.0f);
            }

            yield return OutroResetRoutine();
        }

        private IEnumerator PresentSingleMineralStep(GazeInteractableMineral mineral)
        {
            _activePresentingMineral = mineral;
            mineral.SetPresentationState(true);

            if (sfxAudioSource != null && mineral.Data != null && mineral.Data.activationSfx != null)
            {
                sfxAudioSource.PlayOneShot(mineral.Data.activationSfx);
            }

            float explanationDuration;
            if (mascotController != null && mineral.Data != null)
            {
                explanationDuration = mascotController.ShowMineralExplanation(mineral.Data);
            }
            else
            {
                AudioClip explanationClip = mineral.Data != null ? mineral.Data.mascotExplanationClip : null;
                explanationDuration = PlayMascotVoice(explanationClip, fallbackMineralAudioDuration, triggerWave: false);
            }

            yield return new WaitForSeconds(explanationDuration);

            SetMascotTalkingAnimation(false);
            mineral.SetPresentationState(false);
            _activePresentingMineral = null;
        }

        private IEnumerator OutroResetRoutine()
        {
            currentState = ExperienceState.OutroReset;

            if (_activePresentingMineral != null)
            {
                _activePresentingMineral.SetPresentationState(false);
                _activePresentingMineral = null;
            }

            // Mantém o botão do mascote DESATIVADO para que nada interrompa a fala de Parabéns!
            if (mascotController != null)
            {
                mascotController.SetMascotPrompt(false, string.Empty);
            }

            SetUIState(idleActive: false, outroActive: false);
            onExperienceCompleted?.Invoke();

            float outroClipDuration;
            if (mascotController != null)
            {
                outroClipDuration = mascotController.ShowOutro();
            }
            else
            {
                outroClipDuration = PlayMascotVoice(outroConclusionClip, 7.0f, triggerWave: true);
            }

            // Aguarda toda a fala de Parabéns terminar sem interrupção
            yield return new WaitForSeconds(outroClipDuration);

            SetMascotTalkingAnimation(false);

            // TRAVA na tela de Parabéns! Só reinicia quando tirar o óculos, colocar novamente e aguardar 5s.
            currentState = ExperienceState.CompletedLocked;
            _headsetMovedOffDuringLock = false;
            _offHeadTimer = 0f;
            _steadyOnHeadTimer = 0f;
            if (vrCameraTransform != null)
            {
                _lockedHeadPos = vrCameraTransform.position;
                _lastFrameHeadPos = _lockedHeadPos;
            }

            _activeStateCoroutine = null;
        }

        [ContextMenu("Resetar Experiência (ResetToIdleState)")]
        public void ResetToIdleState()
        {
            StopActiveStateCoroutine();

            currentState = ExperienceState.IdleWaitingUser;
            headsetAdjustmentDelay = 5.0f;
            _adjustmentRemaining = headsetAdjustmentDelay;
            _lastDisplayedCountdownSec = -1;
            _activePresentingMineral = null;
            _headsetMovedOffDuringLock = false;
            _offHeadTimer = 0f;
            _steadyOnHeadTimer = 0f;

            if (mascotAudioSource != null)
            {
                mascotAudioSource.Stop();
            }

            if (mascotController != null)
            {
                mascotController.StopSpeaking();
                mascotController.SetMascotPrompt(false, string.Empty);
                mascotController.ShowStaticMessage(
                    "CÁPSULA DAS TERRAS RARAS - CETEM",
                    $"Acomode os óculos no rosto com conforto!\n\nIniciando a apresentação em {Mathf.CeilToInt(headsetAdjustmentDelay)} segundos...");
            }

            SetMascotTalkingAnimation(false);

            foreach (GazeInteractableMineral mineral in benchMinerals)
            {
                if (mineral != null)
                {
                    mineral.ResetMineralState();
                    mineral.SetInteractable(false);
                }
            }

            if (globalReticleFillImage != null)
            {
                globalReticleFillImage.fillAmount = 0f;
            }

            SetUIState(idleActive: false, outroActive: false);

            onExperienceReset?.Invoke();
        }

        private float PlayMascotVoice(AudioClip clip, float fallbackDuration, bool triggerWave)
        {
            if (mascotAnimator != null && triggerWave)
            {
                mascotAnimator.SetTrigger(AnimTriggerWave);
            }

            SetMascotTalkingAnimation(true);

            if (mascotAudioSource != null && clip != null)
            {
                mascotAudioSource.Stop();
                mascotAudioSource.clip = clip;
                mascotAudioSource.Play();
                return clip.length;
            }

            return fallbackDuration;
        }

        private void SetMascotTalkingAnimation(bool isTalking)
        {
            if (mascotAnimator != null)
            {
                mascotAnimator.SetBool(AnimIsTalking, isTalking);
            }
        }

        private void SetUIState(bool idleActive, bool outroActive)
        {
            if (idleWelcomePanel != null)
            {
                idleWelcomePanel.SetActive(false);
            }

            if (outroConclusionPanel != null)
            {
                outroConclusionPanel.SetActive(false);
            }
        }

        private void StopActiveStateCoroutine()
        {
            if (_activeStateCoroutine != null)
            {
                StopCoroutine(_activeStateCoroutine);
                _activeStateCoroutine = null;
            }
        }
    }
}
