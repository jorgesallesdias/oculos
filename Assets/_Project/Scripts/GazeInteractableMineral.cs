using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CetemVR
{
    /// <summary>
    /// Componente acoplado a cada estação de minério na bancada do laboratório.
    /// Gerencia o destaque visual da imagem real do mineral e a exibição do holograma 3D
    /// da aplicação tecnológica sincronizada com a fala do Mascote durante o tour automático.
    /// </summary>
    public class GazeInteractableMineral : MonoBehaviour
    {
        [Header("Dados do Mineral")]
        [Tooltip("ScriptableObject contendo nome, áudio do mascote, cor e descrição.")]
        [SerializeField] private MineralData mineralData;

        [Header("Configuração do Gaze (Opcional)")]
        [SerializeField] private float gazeActivationTime = 2.0f;
        [SerializeField] private Image radialProgressImage;
        [SerializeField] private GameObject gazeIndicatorRoot;

        [Header("Referências Visuais e Holograma")]
        [Tooltip("Ponto de ancoragem (Transform) acima da estação onde o equipamento 3D será exibido.")]
        [SerializeField] private Transform hologramSpawnPoint;

        [Tooltip("Componente FloatingRotator acoplado à imagem/objeto do mineral para intensificar a flutuação quando ativo.")]
        [SerializeField] private FloatingRotator crystalFloatingRotator;

        [Tooltip("Luz de destaque (Point/Spot Light) da estação.")]
        [SerializeField] private Light highlightLight;

        [Tooltip("Renderer principal do mineral (SpriteRenderer ou MeshRenderer).")]
        [SerializeField] private Renderer crystalRenderer;

        [Header("Eventos Unity")]
        public UnityEvent<GazeInteractableMineral> onMineralSelected;

        public event Action<GazeInteractableMineral> MineralSelected;

        private float _currentGazeTimer;
        private bool _isGazedAt;
        private bool _isInteractable;
        private bool _isCurrentlyPresenting;
        private bool _hasBeenExploredInSession;
        private Vector3 _mineralBaseScale = Vector3.one;

        private GameObject _spawnedHologramInstance;
        private MaterialPropertyBlock _propBlock;
        private static readonly int EmissionColorShaderId = Shader.PropertyToID("_EmissionColor");

        public MineralData Data => mineralData;
        public bool HasBeenExploredInSession => _hasBeenExploredInSession;
        public float NormalizedGazeProgress =>
            gazeActivationTime > 0f ? Mathf.Clamp01(_currentGazeTimer / gazeActivationTime) : 0f;

        private void Awake()
        {
            _propBlock = new MaterialPropertyBlock();

            if (crystalFloatingRotator == null)
            {
                crystalFloatingRotator = GetComponentInChildren<FloatingRotator>();
            }

            if (crystalRenderer != null)
            {
                _mineralBaseScale = crystalRenderer.transform.localScale;
            }

            InitializeHologramInstance();
            ApplyThemeColors();
            ResetMineralState();
        }

        private void Update()
        {
            if (!_isInteractable || _isCurrentlyPresenting)
            {
                return;
            }

            if (_isGazedAt)
            {
                _currentGazeTimer += Time.deltaTime;
                UpdateRadialUI(NormalizedGazeProgress);

                if (_currentGazeTimer >= gazeActivationTime)
                {
                    TriggerSelection();
                }
            }
            else if (_currentGazeTimer > 0f)
            {
                _currentGazeTimer = Mathf.Max(0f, _currentGazeTimer - (Time.deltaTime * 2.5f));
                UpdateRadialUI(NormalizedGazeProgress);

                if (_currentGazeTimer <= 0f && gazeIndicatorRoot != null)
                {
                    gazeIndicatorRoot.SetActive(false);
                }
            }
        }

        public void OnGazeEnter()
        {
            if (!_isInteractable || _isCurrentlyPresenting)
            {
                return;
            }

            _isGazedAt = true;

            if (gazeIndicatorRoot != null)
            {
                gazeIndicatorRoot.SetActive(true);
            }

            if (crystalFloatingRotator != null)
            {
                crystalFloatingRotator.SetBoostedState(true);
            }

            SetCrystalHighlight(true, false);
        }

        public void OnGazeExit()
        {
            _isGazedAt = false;

            if (!_isCurrentlyPresenting)
            {
                if (crystalFloatingRotator != null)
                {
                    crystalFloatingRotator.SetBoostedState(false);
                }
                SetCrystalHighlight(false, false);
            }
        }

        public void SetInteractable(bool interactable)
        {
            _isInteractable = interactable;

            if (!interactable)
            {
                _isGazedAt = false;
                _currentGazeTimer = 0f;
                UpdateRadialUI(0f);

                if (gazeIndicatorRoot != null)
                {
                    gazeIndicatorRoot.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Ativa ou desativa o estado de apresentação (imagem real do mineral destacada + equipamento 3D visível).
        /// </summary>
        public void SetPresentationState(bool active)
        {
            _isCurrentlyPresenting = active;

            if (crystalFloatingRotator != null)
            {
                crystalFloatingRotator.SetBoostedState(active);
            }

            if (_spawnedHologramInstance != null)
            {
                _spawnedHologramInstance.SetActive(active);
            }

            if (highlightLight != null)
            {
                highlightLight.enabled = active;
            }

            SetCrystalHighlight(active, active);

            if (active && gazeIndicatorRoot != null)
            {
                gazeIndicatorRoot.SetActive(false);
            }
        }

        public void ResetMineralState()
        {
            _isGazedAt = false;
            _isCurrentlyPresenting = false;
            _hasBeenExploredInSession = false;
            _currentGazeTimer = 0f;

            UpdateRadialUI(0f);

            if (gazeIndicatorRoot != null)
            {
                gazeIndicatorRoot.SetActive(false);
            }

            if (_spawnedHologramInstance != null)
            {
                _spawnedHologramInstance.SetActive(false);
            }

            if (highlightLight != null)
            {
                highlightLight.enabled = false;
            }

            if (crystalFloatingRotator != null)
            {
                crystalFloatingRotator.ResetTransform();
            }

            SetCrystalHighlight(false, false);
        }

        public void TriggerSelection()
        {
            if (!_isInteractable || _isCurrentlyPresenting)
            {
                return;
            }

            _isGazedAt = false;
            _currentGazeTimer = 0f;
            _hasBeenExploredInSession = true;
            UpdateRadialUI(0f);

            SetPresentationState(true);

            onMineralSelected?.Invoke(this);
            MineralSelected?.Invoke(this);
        }

        private void InitializeHologramInstance()
        {
            if (mineralData != null && mineralData.applicationHologramPrefab != null)
            {
                Transform parentTransform = hologramSpawnPoint != null ? hologramSpawnPoint : transform;
                _spawnedHologramInstance = Instantiate(
                    mineralData.applicationHologramPrefab,
                    parentTransform.position,
                    parentTransform.rotation,
                    parentTransform
                );
            }
            else if (hologramSpawnPoint != null && hologramSpawnPoint.childCount > 0)
            {
                _spawnedHologramInstance = hologramSpawnPoint.GetChild(0).gameObject;
            }

            if (_spawnedHologramInstance != null)
            {
                if (_spawnedHologramInstance.GetComponent<FloatingRotator>() == null &&
                    _spawnedHologramInstance.GetComponent<HologramVisualEffect>() == null)
                {
                    _spawnedHologramInstance.AddComponent<FloatingRotator>();
                }

                _spawnedHologramInstance.SetActive(false);
            }
        }

        private void ApplyThemeColors()
        {
            if (mineralData == null)
            {
                return;
            }

            if (radialProgressImage != null)
            {
                radialProgressImage.color = mineralData.highlightColor;
                radialProgressImage.fillAmount = 0f;
            }

            if (highlightLight != null)
            {
                highlightLight.color = mineralData.highlightColor;
            }
        }

        private void UpdateRadialUI(float normalizedValue)
        {
            if (radialProgressImage != null)
            {
                radialProgressImage.fillAmount = normalizedValue;
            }
        }

        private void SetCrystalHighlight(bool focused, bool presenting)
        {
            if (crystalRenderer == null)
            {
                return;
            }

            float scaleMult = presenting ? 1.22f : (focused ? 1.08f : 1.0f);
            crystalRenderer.transform.localScale = _mineralBaseScale * scaleMult;

            if (crystalRenderer is SpriteRenderer sr)
            {
                sr.color = presenting ? Color.white : new Color(0.92f, 0.95f, 1.0f, 1.0f);
                return;
            }

            if (mineralData != null)
            {
                float intensity = presenting ? 1.5f : (focused ? 0.5f : 0.1f);
                crystalRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(EmissionColorShaderId, mineralData.highlightColor * intensity);
                crystalRenderer.SetPropertyBlock(_propBlock);
            }
        }
    }
}
