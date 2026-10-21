using UnityEngine;

namespace CetemVR
{
    /// <summary>
    /// Animações leves para os equipamentos holográficos:
    /// - Pulse: pulsa a escala (ex.: chama do foguete).
    /// - Sequential: acende os filhos em sequência (ex.: barras de carga da bateria).
    /// </summary>
    public class HologramPulse : MonoBehaviour
    {
        public enum Mode { Pulse, Sequential }

        [SerializeField] private Mode mode = Mode.Pulse;
        [SerializeField] private float speed = 6f;
        [SerializeField] private float amplitude = 0.12f;

        private Vector3 _baseScale;
        private float _seqTimer;
        private int _seqIndex;

        public void Configure(Mode newMode, float newSpeed, float newAmplitude)
        {
            mode = newMode;
            speed = newSpeed;
            amplitude = newAmplitude;
        }

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            _seqTimer = 0f;
            _seqIndex = 0;
        }

        private void Update()
        {
            if (mode == Mode.Pulse)
            {
                float s = 1f + (Mathf.Sin(Time.time * speed) * amplitude) + (Mathf.Sin(Time.time * speed * 2.7f) * amplitude * 0.4f);
                transform.localScale = new Vector3(_baseScale.x * (1f + (s - 1f) * 0.3f), _baseScale.y * s, _baseScale.z * (1f + (s - 1f) * 0.3f));
                return;
            }

            int count = transform.childCount;
            if (count == 0) return;

            _seqTimer += Time.deltaTime * speed;
            if (_seqTimer >= 1f)
            {
                _seqTimer = 0f;
                _seqIndex = (_seqIndex + 1) % (count + 2); // pequena pausa com tudo aceso
            }

            for (int i = 0; i < count; i++)
            {
                transform.GetChild(i).gameObject.SetActive(i < _seqIndex);
            }
        }
    }
}

