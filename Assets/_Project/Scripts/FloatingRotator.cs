using UnityEngine;

namespace CetemVR
{
    /// <summary>
    /// Utilitário leve para aplicar movimento de flutuação vertical (senoide) e rotação contínua
    /// em cristais, amostras minerais e hologramas nos pedestais.
    /// Permite intensificar a velocidade quando o mineral está ativo/selecionado.
    /// </summary>
    public class FloatingRotator : MonoBehaviour
    {
        [Header("Configuração de Flutuação (Senoide)")]
        [Tooltip("Ativa ou desativa o movimento vertical suave.")]
        [SerializeField] private bool enableFloating = true;

        [Tooltip("Amplitude do movimento vertical em metros (ex: 0.03 = 3 cm para cima e para baixo).")]
        [SerializeField] private float floatAmplitude = 0.035f;

        [Tooltip("Frequência (velocidade) da oscilação vertical.")]
        [SerializeField] private float floatFrequency = 1.5f;

        [Header("Configuração de Rotação")]
        [Tooltip("Ativa ou desativa a rotação contínua do objeto.")]
        [SerializeField] private bool enableRotation = true;

        [Tooltip("Velocidade de rotação em graus por segundo em cada eixo (X, Y, Z).")]
        [SerializeField] private Vector3 rotationSpeed = new Vector3(0f, 25f, 0f);

        [Header("Modo Destaque (Quando o Mineral é Ativado)")]
        [Tooltip("Multiplicador de velocidade de rotação e flutuação quando o mineral está sendo explicado.")]
        [SerializeField] private float boostedMultiplier = 2.2f;

        [Tooltip("Elevação extra (em metros) quando o mineral está ativo em exposição.")]
        [SerializeField] private float activeHeightOffset = 0.08f;

        [Tooltip("Velocidade de transição suave entre o estado normal e o estado elevado.")]
        [SerializeField] private float transitionLerpSpeed = 4.0f;

        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private float _randomPhaseOffset;
        private bool _isBoosted;
        private float _currentMultiplier = 1f;
        private float _currentHeightOffset;
        private bool _initialized;

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialLocalPosition = transform.localPosition;
            _initialLocalRotation = transform.localRotation;

            // Deslocamento aleatório de fase para que os 4 cristais na bancada não subam/desçam em sincronia artificial
            _randomPhaseOffset = Random.Range(0f, Mathf.PI * 2f);
            _initialized = true;
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            EnsureInitialized();

            float targetMultiplier = _isBoosted ? boostedMultiplier : 1f;
            float targetHeight = _isBoosted ? activeHeightOffset : 0f;

            _currentMultiplier = Mathf.Lerp(_currentMultiplier, targetMultiplier, Time.deltaTime * transitionLerpSpeed);
            _currentHeightOffset = Mathf.Lerp(_currentHeightOffset, targetHeight, Time.deltaTime * transitionLerpSpeed);

            if (enableFloating)
            {
                float sineWave = Mathf.Sin((Time.time * floatFrequency * _currentMultiplier) + _randomPhaseOffset);
                float yOffset = (sineWave * floatAmplitude) + _currentHeightOffset;
                transform.localPosition = _initialLocalPosition + new Vector3(0f, yOffset, 0f);
            }

            if (enableRotation)
            {
                transform.Rotate(rotationSpeed * (_currentMultiplier * Time.deltaTime), Space.Self);
            }
        }

        /// <summary>
        /// Define dinamicamente os parâmetros de rotação e flutuação.
        /// </summary>
        public void SetRotatorProperties(Vector3 speed, float amplitude = 0.02f, float frequency = 1.5f)
        {
            rotationSpeed = speed;
            floatAmplitude = amplitude;
            floatFrequency = frequency;
            enableFloating = amplitude > 0f;
            enableRotation = speed != Vector3.zero;
        }

        /// <summary>
        /// Ativa ou desativa o modo destacado (flutuação mais alta e giro mais rápido durante a explicação do mascote).
        /// </summary>
        public void SetBoostedState(bool boosted)
        {
            EnsureInitialized();
            _isBoosted = boosted;
        }

        /// <summary>
        /// Restaura imediatamente a posição e rotação iniciais do objeto (usado no Auto-Reset da cena).
        /// </summary>
        public void ResetTransform()
        {
            EnsureInitialized();
            _isBoosted = false;
            _currentMultiplier = 1f;
            _currentHeightOffset = 0f;
            transform.localPosition = _initialLocalPosition;
            transform.localRotation = _initialLocalRotation;
        }
    }
}

