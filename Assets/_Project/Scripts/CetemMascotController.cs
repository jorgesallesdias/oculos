using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CetemVR
{
    /// <summary>
    /// Controlador do mascote oficial do CETEM (personagem 2D recortado em partes).
    /// - Reproduz a voz falada natural em português (AudioClip) em ritmo claro e agradável.
    /// - Lip-sync real: a boca abre e fecha acompanhando a amplitude da voz falada.
    /// - Exibe um ÚNICO painel de fala/notícia grande ao lado do mascote (sem nenhum painel duplicado atrás),
    ///   com tempo extra de leitura (4,5s após terminar de falar cada mineral) para leitura tranquila.
    /// </summary>
    public class CetemMascotController : MonoBehaviour
    {
        [Header("Partes do Personagem")]
        [SerializeField] private Transform mascotVisual;
        [SerializeField] private Transform waveArm;
        [SerializeField] private Transform mouth;
        [SerializeField] private Transform eyelidLeft;
        [SerializeField] private Transform eyelidRight;
        [SerializeField] private Transform vrCameraTransform;

        [Header("Placa Principal de Fala / Notícia")]
        [SerializeField] private GameObject speechBubbleRoot;
        [SerializeField] private Text speechTitleText;
        [SerializeField] private Text speechBodyText;
        [SerializeField] private CanvasGroup speechBubbleCanvasGroup;

        [Header("Botão / Crachá Interativo no Mascote")]
        [SerializeField] private GameObject startBadgeRoot;
        [SerializeField] private Text startBadgeLabel;
        [SerializeField] private Image startBadgeRadialFill;
        [SerializeField] private float gazeActivationTime = 2.5f;

        [Header("Áudio de Voz Natural")]
        [SerializeField] private AudioSource voiceSource;
        [SerializeField] private AudioClip introVoiceClip;
        [SerializeField] private AudioClip outroVoiceClip;

        [Tooltip("Velocidade da voz do mascote (1.06 = ritmo natural e dinâmico sem correr).")]
        [SerializeField] private float voiceSpeed = 1.06f;

        public event Action MascotClicked;

        private Vector3 _visualBaseScale;
        private Vector3 _visualBasePos;
        private Quaternion _armBaseRot;
        private Vector3 _mouthBaseScale;
        private Vector3 _lidLBaseScale;
        private Vector3 _lidRBaseScale;

        private bool _isTalking;
        private float _mouthTarget;
        private float _mouthCurrent;
        private float _talkBounce;
        private float _waveTimer;
        private float _hopTimer;
        private float _nextBlinkTime;
        private float _blinkTimer = -1f;

        private bool _isGazedAt;
        private bool _isInteractable;
        private float _gazeTimer;

        private Coroutine _speechRoutine;
        private readonly float[] _sampleWindow = new float[256];

        public bool IsTalking => _isTalking;
        public bool IsInteractable => _isInteractable;
        public float NormalizedGazeProgress =>
            gazeActivationTime > 0f ? Mathf.Clamp01(_gazeTimer / gazeActivationTime) : 0f;

        private void Awake()
        {
            if (mascotVisual == null) mascotVisual = transform;

            _visualBaseScale = mascotVisual.localScale;
            _visualBasePos = mascotVisual.localPosition;
            if (waveArm != null) _armBaseRot = waveArm.localRotation;
            if (mouth != null) _mouthBaseScale = mouth.localScale;
            if (eyelidLeft != null) _lidLBaseScale = eyelidLeft.localScale;
            if (eyelidRight != null) _lidRBaseScale = eyelidRight.localScale;

            SetMouthOpen(0f);
            SetEyelids(0f);
            _nextBlinkTime = Time.time + UnityEngine.Random.Range(1.5f, 3.5f);

            if (startBadgeRadialFill != null) startBadgeRadialFill.fillAmount = 0f;
        }

        private void Start()
        {
            if (vrCameraTransform == null && Camera.main != null)
            {
                vrCameraTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            FaceVisitor();
            UpdateGazeTimer();
            UpdateVoiceLipSync();
            AnimateBody();
            AnimateMouth();
            AnimateBlink();
            AnimateArm();
        }

        // ------------------------------------------------------------------
        // Interação (Olhar por 2,5s ou Clique no Mascote para Iniciar)
        // ------------------------------------------------------------------

        private void UpdateGazeTimer()
        {
            if (!_isInteractable)
            {
                if (_gazeTimer > 0f)
                {
                    _gazeTimer = 0f;
                    if (startBadgeRadialFill != null) startBadgeRadialFill.fillAmount = 0f;
                }
                return;
            }

            if (_isGazedAt)
            {
                _gazeTimer += Time.deltaTime;
                if (startBadgeRadialFill != null) startBadgeRadialFill.fillAmount = NormalizedGazeProgress;

                if (_gazeTimer >= gazeActivationTime)
                {
                    TriggerMascotClick();
                }
            }
            else if (_gazeTimer > 0f)
            {
                _gazeTimer = Mathf.Max(0f, _gazeTimer - Time.deltaTime * 2.0f);
                if (startBadgeRadialFill != null) startBadgeRadialFill.fillAmount = NormalizedGazeProgress;
            }
        }

        public void OnGazeEnter()
        {
            if (!_isInteractable) return;
            _isGazedAt = true;
            TriggerWave();
        }

        public void OnGazeExit()
        {
            _isGazedAt = false;
        }

        public void TriggerMascotClick()
        {
            if (!_isInteractable) return;
            _isGazedAt = false;
            _gazeTimer = 0f;
            if (startBadgeRadialFill != null) startBadgeRadialFill.fillAmount = 0f;

            TriggerHop();
            TriggerWave();
            MascotClicked?.Invoke();
        }

        public void SetMascotPrompt(bool interactable, string badgeText)
        {
            _isInteractable = interactable;
            if (!interactable)
            {
                _isGazedAt = false;
                _gazeTimer = 0f;
                if (startBadgeRadialFill != null) startBadgeRadialFill.fillAmount = 0f;
            }

            if (startBadgeRoot != null)
            {
                startBadgeRoot.SetActive(!string.IsNullOrEmpty(badgeText));
            }
            if (startBadgeLabel != null && !string.IsNullOrEmpty(badgeText))
            {
                startBadgeLabel.text = badgeText;
            }
        }

        /// <summary>
        /// Exibe uma mensagem estática na placa principal ao lado do Mascote (sem voz),
        /// garantindo que exista apenas UMA placa na cena (sem nunca sobrepor outra placa atrás).
        /// </summary>
        public void ShowStaticMessage(string title, string body)
        {
            if (_speechRoutine != null)
            {
                StopCoroutine(_speechRoutine);
                _speechRoutine = null;
            }
            if (voiceSource != null) voiceSource.Stop();
            _isTalking = false;
            _mouthTarget = 0f;

            if (speechBubbleRoot != null) speechBubbleRoot.SetActive(true);
            if (speechBubbleCanvasGroup != null) speechBubbleCanvasGroup.alpha = 1f;
            if (speechTitleText != null) speechTitleText.text = title ?? string.Empty;
            if (speechBodyText != null) speechBodyText.text = body ?? string.Empty;
        }

        // ------------------------------------------------------------------
        // API pública de Fala
        // ------------------------------------------------------------------

        public float Speak(string title, string body, AudioClip voiceClip, float extraHoldSeconds = 4.0f, bool wave = true, bool hideBubbleAtEnd = false)
        {
            if (_speechRoutine != null) StopCoroutine(_speechRoutine);

            float speed = Mathf.Clamp(voiceSpeed, 0.95f, 1.25f);
            float clipLen = voiceClip != null
                ? (voiceClip.length / speed)
                : Mathf.Max(5.0f, (body != null ? body.Length : 40) / 16f);

            _speechRoutine = StartCoroutine(SpeakRoutine(title, body, voiceClip, speed, clipLen, extraHoldSeconds, hideBubbleAtEnd));

            if (wave) TriggerWave();
            return clipLen + extraHoldSeconds;
        }

        public void StopSpeaking()
        {
            if (_speechRoutine != null)
            {
                StopCoroutine(_speechRoutine);
                _speechRoutine = null;
            }
            if (voiceSource != null) voiceSource.Stop();
            _isTalking = false;
            _mouthTarget = 0f;
        }

        public void TriggerWave()
        {
            _waveTimer = 2.2f;
        }

        public void TriggerHop()
        {
            _hopTimer = 0.45f;
        }

        public float ShowDefaultWelcome()
        {
            TriggerHop();
            return Speak(
                "Bem-vindo ao CETEM!",
                "Vamos conhecer 4 minerais estratégicos do Brasil que movem a tecnologia do futuro!",
                introVoiceClip,
                extraHoldSeconds: 2.5f,
                wave: true,
                hideBubbleAtEnd: false);
        }

        public float ShowMineralExplanation(MineralData data)
        {
            if (data == null) return 0f;
            TriggerHop();
            string title = $"{data.mineralName} ({data.chemicalSymbol})";
            string body = data.shortDescription;
            // Mantém a placa com a explicação e o holograma por mais 4,5 segundos após terminar a fala para leitura tranquila!
            return Speak(title, body, data.mascotExplanationClip, extraHoldSeconds: 4.5f, wave: true, hideBubbleAtEnd: false);
        }

        public float ShowOutro()
        {
            TriggerHop();
            return Speak(
                "Parabéns, explorador!",
                "Você conheceu os 4 minerais estratégicos do futuro aqui no CETEM!\nObrigado pela visita! Retire os óculos com cuidado.",
                outroVoiceClip,
                extraHoldSeconds: 2.0f,
                wave: true,
                hideBubbleAtEnd: false);
        }

        // ------------------------------------------------------------------
        // Rotina de fala sincronizada com voz real
        // ------------------------------------------------------------------

        private IEnumerator SpeakRoutine(string title, string body, AudioClip voiceClip, float speed, float speechDuration, float holdSeconds, bool hideBubbleAtEnd)
        {
            if (speechBubbleRoot != null) speechBubbleRoot.SetActive(true);
            if (speechBubbleCanvasGroup != null) speechBubbleCanvasGroup.alpha = 1f;
            if (speechTitleText != null) speechTitleText.text = title;

            body = body ?? string.Empty;
            if (speechBodyText != null) speechBodyText.text = body;

            if (voiceSource != null)
            {
                voiceSource.Stop();
                if (voiceClip != null)
                {
                    voiceSource.clip = voiceClip;
                    voiceSource.pitch = speed;
                    voiceSource.Play();
                }
            }

            _isTalking = true;
            float elapsed = 0f;
            while (elapsed < speechDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            _isTalking = false;
            _mouthTarget = 0f;

            // Tempo extra com a placa visível para a pessoa terminar de ler com calma
            yield return new WaitForSeconds(holdSeconds);

            if (hideBubbleAtEnd && speechBubbleRoot != null)
            {
                speechBubbleRoot.SetActive(false);
            }

            _speechRoutine = null;
        }

        private void UpdateVoiceLipSync()
        {
            if (!_isTalking)
            {
                _mouthTarget = 0f;
                return;
            }

            if (voiceSource != null && voiceSource.isPlaying && voiceSource.clip != null)
            {
                AudioClip clip = voiceSource.clip;
                int pos = voiceSource.timeSamples;
                if (pos >= 0 && pos + _sampleWindow.Length < clip.samples && clip.loadType == AudioClipLoadType.DecompressOnLoad)
                {
                    clip.GetData(_sampleWindow, pos);
                    float sum = 0f;
                    for (int i = 0; i < _sampleWindow.Length; i++)
                    {
                        float s = _sampleWindow[i];
                        sum += s * s;
                    }
                    float rms = Mathf.Sqrt(sum / _sampleWindow.Length);
                    float open = Mathf.Clamp01(rms * 5.5f);
                    _mouthTarget = open;
                    if (open > 0.32f) _talkBounce = Mathf.Max(_talkBounce, open * 0.75f);
                    return;
                }
            }

            float wave = Mathf.Abs(Mathf.Sin(Time.time * 11f)) * (0.4f + 0.6f * Mathf.PerlinNoise(Time.time * 6f, 0f));
            _mouthTarget = wave;
            if (wave > 0.5f) _talkBounce = 0.5f;
        }

        // ------------------------------------------------------------------
        // Animações procedurais
        // ------------------------------------------------------------------

        private void FaceVisitor()
        {
            if (vrCameraTransform == null)
            {
                if (Camera.main == null) return;
                vrCameraTransform = Camera.main.transform;
            }

            Vector3 away = transform.position - vrCameraTransform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(away.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * 3f);
            }
        }

        private void AnimateBody()
        {
            float t = Time.time;
            float breathe = Mathf.Sin(t * 2.2f) * 0.02f;

            _talkBounce = Mathf.MoveTowards(_talkBounce, 0f, Time.deltaTime * 5.5f);
            float talkSquash = _talkBounce * 0.045f;

            float hopY = 0f;
            if (_hopTimer > 0f)
            {
                _hopTimer -= Time.deltaTime;
                float p = 1f - Mathf.Clamp01(_hopTimer / 0.45f);
                hopY = Mathf.Sin(p * Mathf.PI) * 0.085f;
            }

            float scaleY = 1f + breathe + talkSquash;
            float scaleX = 1f - (breathe * 0.5f) - (talkSquash * 0.5f);
            mascotVisual.localScale = new Vector3(_visualBaseScale.x * scaleX, _visualBaseScale.y * scaleY, _visualBaseScale.z);

            float swayAmp = _isTalking ? 3.8f : 1.5f;
            float sway = Mathf.Sin(t * (_isTalking ? 3.2f : 1.3f)) * swayAmp;
            mascotVisual.localRotation = Quaternion.Euler(0f, 0f, sway);

            float floatY = Mathf.Sin(t * 1.7f) * 0.01f;
            mascotVisual.localPosition = _visualBasePos + new Vector3(0f, floatY + hopY, 0f);
        }

        private void AnimateMouth()
        {
            if (!_isTalking) _mouthTarget = 0f;
            _mouthCurrent = Mathf.Lerp(_mouthCurrent, _mouthTarget, Time.deltaTime * 24f);
            SetMouthOpen(_mouthCurrent);
        }

        private void SetMouthOpen(float amount)
        {
            if (mouth == null) return;
            bool visible = amount > 0.08f;
            if (mouth.gameObject.activeSelf != visible) mouth.gameObject.SetActive(visible);
            mouth.localScale = new Vector3(
                _mouthBaseScale.x * Mathf.Lerp(0.85f, 1.05f, amount),
                _mouthBaseScale.y * Mathf.Max(0.18f, amount),
                _mouthBaseScale.z);
        }

        private void AnimateBlink()
        {
            if (_blinkTimer < 0f && Time.time >= _nextBlinkTime)
            {
                _blinkTimer = 0f;
            }

            if (_blinkTimer >= 0f)
            {
                _blinkTimer += Time.deltaTime;
                const float blinkDuration = 0.16f;
                float p = _blinkTimer / blinkDuration;
                float closed = p < 0.5f ? p * 2f : (1f - p) * 2f;
                SetEyelids(Mathf.Clamp01(closed));

                if (_blinkTimer >= blinkDuration)
                {
                    _blinkTimer = -1f;
                    SetEyelids(0f);
                    _nextBlinkTime = Time.time + UnityEngine.Random.Range(2.2f, 5.2f);
                }
            }
        }

        private void SetEyelids(float closed)
        {
            ApplyLid(eyelidLeft, _lidLBaseScale, closed);
            ApplyLid(eyelidRight, _lidRBaseScale, closed);
        }

        private static void ApplyLid(Transform lid, Vector3 baseScale, float closed)
        {
            if (lid == null) return;
            bool visible = closed > 0.02f;
            if (lid.gameObject.activeSelf != visible) lid.gameObject.SetActive(visible);
            lid.localScale = new Vector3(baseScale.x, baseScale.y * Mathf.Max(0.01f, closed), baseScale.z);
        }

        private void AnimateArm()
        {
            if (waveArm == null) return;

            float angle;
            if (_waveTimer > 0f)
            {
                _waveTimer -= Time.deltaTime;
                angle = Mathf.Sin(Time.time * 12f) * 24f;
            }
            else if (_isTalking)
            {
                angle = Mathf.Sin(Time.time * 4.6f) * 11f;
            }
            else
            {
                angle = Mathf.Sin(Time.time * 1.6f) * 4.0f;
            }

            Quaternion target = _armBaseRot * Quaternion.Euler(0f, 0f, angle);
            waveArm.localRotation = Quaternion.Slerp(waveArm.localRotation, target, Time.deltaTime * 14f);
        }
    }
}
