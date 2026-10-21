using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace CetemVR
{
    /// <summary>
    /// Garante que o cenário (bancada, 4 minerais, mascote e painéis) esteja SEMPRE
    /// na distância e altura ergonômicas exatas em relação à cabeça do visitante no Quest 2,
    /// independentemente de onde a cadeira/visitante esteja dentro do limite Guardian.
    /// </summary>
    public class ExperienceAnchor : MonoBehaviour
    {
        [SerializeField] private Transform vrCameraTransform;

        [Tooltip("Altura dos olhos usada como referência na montagem da cena (metros).")]
        [SerializeField] private float designEyeHeight = 1.55f;

        private readonly List<XRInputSubsystem> _subsystems = new List<XRInputSubsystem>();
        private bool _hasAlignedYaw;
        private bool _wasHeadsetWorn;
        private int _trackedFrames;

        private void Awake()
        {
            if (vrCameraTransform == null && Camera.main != null)
            {
                vrCameraTransform = Camera.main.transform;
            }
        }

        private void OnEnable()
        {
            SubsystemManager.GetSubsystems(_subsystems);
            foreach (var s in _subsystems)
            {
                if (s != null) s.trackingOriginUpdated += OnTrackingOriginUpdated;
            }
        }

        private void OnDisable()
        {
            foreach (var s in _subsystems)
            {
                if (s != null) s.trackingOriginUpdated -= OnTrackingOriginUpdated;
            }
        }

        private void LateUpdate()
        {
            if (vrCameraTransform == null)
            {
                if (Camera.main == null) return;
                vrCameraTransform = Camera.main.transform;
            }

            bool isTracked = IsHeadTracked();
            bool isWorn = IsHeadsetWorn();

            // Quando o visitante coloca o óculos no rosto ou nos primeiros frames rastreados, alinha direção + posição
            if (isTracked)
            {
                _trackedFrames++;
                if (!_hasAlignedYaw || _trackedFrames == 5 || _trackedFrames == 30 || (isWorn && !_wasHeadsetWorn))
                {
                    Recenter();
                    _hasAlignedYaw = true;
                }
            }

            _wasHeadsetWorn = isWorn;

            // Mantém a distância e a altura da bancada sempre constantes caso o Guardian desloque a câmera
            Vector3 head = vrCameraTransform.position;
            Vector3 targetPos = new Vector3(head.x, head.y - designEyeHeight, head.z);
            if ((transform.position - targetPos).sqrMagnitude > 0.04f) // > 20 cm de desvio
            {
                transform.position = targetPos;
                Physics.SyncTransforms();
            }
        }

        private void OnTrackingOriginUpdated(XRInputSubsystem subsystem)
        {
            Recenter();
        }

        /// <summary>
        /// Posiciona e rotaciona o cenário à frente de onde o visitante está olhando.
        /// </summary>
        [ContextMenu("Recentralizar Cenário")]
        public void Recenter()
        {
            if (vrCameraTransform == null) return;

            Vector3 head = vrCameraTransform.position;
            Vector3 forward = vrCameraTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

            transform.SetPositionAndRotation(
                new Vector3(head.x, head.y - designEyeHeight, head.z),
                Quaternion.LookRotation(forward.normalized, Vector3.up));

            Physics.SyncTransforms();
        }

        private static bool IsHeadTracked()
        {
            InputDevice centerEye = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (centerEye.isValid && centerEye.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked)
            {
                return true;
            }
            InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            return head.isValid && head.TryGetFeatureValue(CommonUsages.isTracked, out bool hTracked) && hTracked;
        }

        private static bool IsHeadsetWorn()
        {
            InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            return head.isValid && head.TryGetFeatureValue(CommonUsages.userPresence, out bool present) && present;
        }
    }
}
