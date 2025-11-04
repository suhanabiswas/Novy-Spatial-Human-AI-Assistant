using System.Collections.Generic;
using UnityEngine;


namespace UnityEngine.XR.Interaction.Toolkit.Samples.Hands
{
    /// <summary>
    /// Controls a UI menu and toggles recording buttons in front of the user,
    /// and enables/disables interactable objects during recording.
    /// </summary>
    public class ToggleUIGameObject : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The GameObject to activate as the UI menu.")]
        GameObject m_ActivationGameObject;

        public GameObject activationGameObject
        {
            get => m_ActivationGameObject;
            set => m_ActivationGameObject = value;
        }

        [SerializeField]
        [Tooltip("Main camera reference (assign in Inspector).")]
        Camera mainCamera;

        [SerializeField]
        [Tooltip("Distance in front of the camera to place the UI menu.")]
        float distanceFromCamera = 1f;

        [SerializeField]
        [Tooltip("The Start Recording button GameObject.")]
        GameObject startRecordingButton;

        [SerializeField]
        [Tooltip("The Stop Recording button GameObject.")]
        GameObject stopRecordingButton;

        /*List<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable> interactables = new List<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();

        void Start()
        {
            // Find and store all XRSimpleInteractable (inheriting from XRBaseInteractable)
            var all = FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>(true);
            foreach (var i in all)
            {
                if (i is UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable)
                    interactables.Add(i);
            }

            // Disable interactables at startup
            SetInteractablesActive(false);
        }*/

        /// <summary>
        /// Called to show the menu and switch to stop recording state.
        /// </summary>
        public void StopRecordingButtonPressed()
        {
            ShowMenu();

            if (startRecordingButton != null)
                startRecordingButton.SetActive(true);

            if (stopRecordingButton != null)
                stopRecordingButton.SetActive(false);

            //SetInteractablesActive(false);
        }

        /// <summary>
        /// Called to start recording and enable interactables.
        /// </summary>
        public void StartRecordingButtonPressed()
        {
            if (startRecordingButton != null)
                startRecordingButton.SetActive(false);

            if (stopRecordingButton != null)
                stopRecordingButton.SetActive(true);

            //SetInteractablesActive(true);
        }

        public void ShowMenu()
        {
            if (activationGameObject == null || mainCamera == null)
                return;

            activationGameObject.SetActive(true);
            PositionInFrontOfCamera();
        }

        void PositionInFrontOfCamera()
        {
            Vector3 forward = mainCamera.transform.forward;
            Vector3 targetPosition = mainCamera.transform.position + forward * distanceFromCamera;

            activationGameObject.transform.position = targetPosition;

            // Look at camera using only yaw
            Vector3 lookDirection = mainCamera.transform.position - targetPosition;
            lookDirection.y = 0;

            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(-lookDirection.normalized);
                activationGameObject.transform.rotation = targetRotation;
            }
        }

        /*void SetInteractablesActive(bool isActive)
        {
            foreach (var interactable in interactables)
            {
                if (interactable != null)
                    interactable.enabled = isActive;
            }
        }*/
    }
}
