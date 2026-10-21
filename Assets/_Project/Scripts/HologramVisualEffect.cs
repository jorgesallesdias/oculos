using UnityEngine;

namespace CetemVR
{
    /// <summary>
    /// Aplica animação suave de surgimento elástico, flutuação e rotação contínua
    /// aos hologramas tecnológicos de cada mineral.
    /// </summary>
    public class HologramVisualEffect : MonoBehaviour
    {
        [Header("Animação de Inicialização")]
        [Tooltip("Duração da transição de escala ao surgir (em segundos).")]
        [SerializeField] private float appearDuration = 0.5f;

        [Tooltip("Escala final do holograma.")]
        [SerializeField] private Vector3 targetScale = Vector3.zero;

        [Header("Rotação e Flutuação Contínua")]
        [SerializeField] private Vector3 rotationSpeed = new Vector3(0f, 25f, 0f);
        [SerializeField] private float floatAmplitude = 0.015f;
        [SerializeField] private float floatFrequency = 1.8f;

        private Vector3 _initialLocalPosition;
        private Vector3 _resolvedTargetScale;
        private float _appearTimer;
        private bool _isAppearing;

        private void Awake()
        {
            _initialLocalPosition = transform.localPosition;

            // Se targetScale não foi definida manualmente no Inspector, utiliza a escala natural da malha
            if (targetScale == Vector3.zero)
            {
                _resolvedTargetScale = transform.localScale;
                if (_resolvedTargetScale == Vector3.zero)
                {
                    _resolvedTargetScale = Vector3.one * 0.28f;
                }
            }
            else
            {
                _resolvedTargetScale = targetScale;
            }
        }

        private void OnEnable()
        {
            // Inicia em zero e cresce suavemente até a escala correta
            transform.localScale = Vector3.zero;
            _appearTimer = 0f;
            _isAppearing = true;
        }

        private void Update()
        {
            if (_isAppearing)
            {
                _appearTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(_appearTimer / appearDuration);
                
                // Curva suave de aproximação (ease-out cubic)
                float scaleFactor = 1.0f - Mathf.Pow(1.0f - progress, 3f);
                transform.localScale = _resolvedTargetScale * scaleFactor;

                if (progress >= 1.0f)
                {
                    transform.localScale = _resolvedTargetScale;
                    _isAppearing = false;
                }
            }

            // Flutuação vertical suave
            float yOffset = Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
            transform.localPosition = _initialLocalPosition + new Vector3(0f, yOffset, 0f);

            // Rotação contínua elegante
            transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);
        }

        /// <summary>
        /// Define dinamicamente o tamanho alvo do holograma.
        /// </summary>
        public void SetTargetScale(Vector3 newScale)
        {
            targetScale = newScale;
            _resolvedTargetScale = newScale;
            if (!_isAppearing)
            {
                transform.localScale = newScale;
            }
        }
    }
}
