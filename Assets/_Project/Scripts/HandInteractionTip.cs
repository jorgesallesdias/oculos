using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

namespace CetemVR
{
    /// <summary>
    /// Ponteiro para os controles ou mãos livres (Hand Tracking) do Meta Quest 2:
    /// - Interage EXCLUSIVAMENTE com o Mascote do CETEM (para iniciar ou reiniciar o vídeo/apresentação),
    ///   e somente após o período inicial de 10s para ajuste do óculos no rosto.
    /// - Exige clique/gatilho/pinça intencional (sem disparo automático apenas por levantar as mãos para ajeitar o óculos).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class HandInteractionTip : MonoBehaviour
    {
        [Header("Configuração da Mão")]
        [SerializeField] private XRNode handNode = XRNode.RightHand;

        [Header("Interação com o Mascote")]
        [SerializeField] private bool enablePinchRay = true;
        [SerializeField] private float rayDistance = 6.0f;
        [SerializeField] private LayerMask interactableLayerMask = ~0;
        [SerializeField] private LineRenderer pointerLineRenderer;

        private InputDevice _handDevice;
        private XRHandSubsystem _handSubsystem;
        private readonly List<XRHandSubsystem> _subsystems = new List<XRHandSubsystem>();

        private bool _wasPinchingLastFrame;
        private bool _isBareHandPinching;
        private Collider _collider;
        private Renderer _renderer;
        private bool _isTracked;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _collider.isTrigger = true;
            _renderer = GetComponent<Renderer>();

            if (pointerLineRenderer == null)
            {
                pointerLineRenderer = GetComponent<LineRenderer>();
            }

            SetTrackedVisuals(false);
        }

        private void Update()
        {
            bool tracked = UpdateDeviceOrBareHandPose();
            if (tracked != _isTracked)
            {
                SetTrackedVisuals(tracked);
            }

            if (tracked && enablePinchRay)
            {
                UpdatePointerRaycast();
            }
            else if (pointerLineRenderer != null)
            {
                pointerLineRenderer.enabled = false;
            }
        }

        private void InitializeHandDevice()
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesAtXRNode(handNode, devices);
            if (devices.Count > 0)
            {
                _handDevice = devices[0];
            }
        }

        private XRHandSubsystem GetHandSubsystem()
        {
            if (_handSubsystem != null && _handSubsystem.running)
            {
                return _handSubsystem;
            }

            SubsystemManager.GetSubsystems(_subsystems);
            for (int i = 0; i < _subsystems.Count; i++)
            {
                if (_subsystems[i] != null && _subsystems[i].running)
                {
                    _handSubsystem = _subsystems[i];
                    return _handSubsystem;
                }
            }
            return null;
        }

        private bool UpdateDeviceOrBareHandPose()
        {
            _isBareHandPinching = false;

            if (!_handDevice.isValid)
            {
                InitializeHandDevice();
            }

            if (_handDevice.isValid && _handDevice.TryGetFeatureValue(CommonUsages.isTracked, out bool devTracked) && devTracked)
            {
                if (_handDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos))
                {
                    transform.localPosition = pos;
                }
                if (_handDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot))
                {
                    transform.localRotation = rot;
                }
                return true;
            }

            XRHandSubsystem handSub = GetHandSubsystem();
            if (handSub != null)
            {
                XRHand hand = handNode == XRNode.LeftHand ? handSub.leftHand : handSub.rightHand;
                if (hand.isTracked)
                {
                    XRHandJoint indexTip = hand.GetJoint(XRHandJointID.IndexTip);
                    XRHandJoint wrist = hand.GetJoint(XRHandJointID.Wrist);
                    XRHandJoint thumbTip = hand.GetJoint(XRHandJointID.ThumbTip);

                    if (indexTip.TryGetPose(out Pose indexPose))
                    {
                        transform.localPosition = indexPose.position;

                        if (wrist.TryGetPose(out Pose wristPose))
                        {
                            Vector3 dir = (indexPose.position - wristPose.position);
                            if (dir.sqrMagnitude > 0.0001f)
                            {
                                transform.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                            }
                            else
                            {
                                transform.localRotation = indexPose.rotation;
                            }
                        }
                        else
                        {
                            transform.localRotation = indexPose.rotation;
                        }

                        if (thumbTip.TryGetPose(out Pose thumbPose))
                        {
                            float pinchDist = Vector3.Distance(indexPose.position, thumbPose.position);
                            _isBareHandPinching = pinchDist < 0.025f;
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        private void SetTrackedVisuals(bool tracked)
        {
            _isTracked = tracked;
            if (_collider != null) _collider.enabled = tracked;
            if (_renderer != null) _renderer.enabled = false;
            if (pointerLineRenderer != null) pointerLineRenderer.enabled = false;
        }

        private void UpdatePointerRaycast()
        {
            Physics.SyncTransforms();

            Ray handRay = new Ray(transform.position, transform.forward);
            CetemMascotController hitMascot = null;
            Vector3 end = handRay.origin + handRay.direction * 1.5f;

            if (Physics.Raycast(handRay, out RaycastHit hitInfo, rayDistance, interactableLayerMask, QueryTriggerInteraction.Collide))
            {
                var candidate = hitInfo.collider.GetComponentInParent<CetemMascotController>();
                if (candidate != null && candidate.IsInteractable)
                {
                    hitMascot = candidate;
                    end = hitInfo.point;
                }
            }

            bool showLine = hitMascot != null;
            if (pointerLineRenderer != null)
            {
                pointerLineRenderer.enabled = showLine;
                if (showLine)
                {
                    pointerLineRenderer.SetPosition(0, handRay.origin);
                    pointerLineRenderer.SetPosition(1, end);
                    Color c = Color.cyan;
                    pointerLineRenderer.startColor = c;
                    pointerLineRenderer.endColor = new Color(c.r, c.g, c.b, 0.1f);
                }
            }

            if (_renderer != null)
            {
                _renderer.enabled = showLine;
            }

            bool isPinching = CheckIsPinching();
            if (isPinching && !_wasPinchingLastFrame && hitMascot != null)
            {
                hitMascot.TriggerMascotClick();
            }
            _wasPinchingLastFrame = isPinching;
        }

        private bool CheckIsPinching()
        {
            if (_isBareHandPinching) return true;
            if (_handDevice.isValid)
            {
                if (_handDevice.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue) && triggerValue > 0.55f) return true;
                if (_handDevice.TryGetFeatureValue(CommonUsages.grip, out float gripValue) && gripValue > 0.65f) return true;
                if (_handDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool buttonPressed) && buttonPressed) return true;
                if (_handDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out bool secPressed) && secPressed) return true;
            }
            return false;
        }
    }
}
