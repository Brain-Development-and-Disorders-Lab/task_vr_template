using UnityEngine;
using System;
using System.Collections;
using System.Linq;

namespace Core
{
    /// <summary>
    /// Manager for headset setup operations. Currently handles setup, operation, and eye-tracking calibration.
    /// Calibration has two phases:
    /// 1. Setup - Move the fixation object around the screen to collect data
    /// 2. Validation - Move the fixation object around the screen to validate the data, with a smaller threshold
    /// </summary>
    public class SetupManager : MonoBehaviour
    {
        [Header("Required visual elements")]
        [SerializeField]
        private GameObject _viewCalibrationPrefab; // Prefab containing visual elements aiding in calibration procedure
        [SerializeField]
        private GameObject _stimulusAnchor;
        private GameObject _viewCalibrationPrefabInstance;

        // `GazeManager` object
        private PresentationManager _presentationManager;

        // Flags for state management
        private bool _fixationCanProceed = false; // 'true' when the fixation object can proceed to the next position
        private bool _isEyeTrackingCalibrationActive = false; // 'true' when running calibration operations
        private bool _isEyeTrackingCalibrationSetup = false; // 'true' once the eye tracking calibration is validated
        private bool _isEyeTrackingCalibrationComplete = false; // 'true' once operations complete

        // Set of points to be displayed for fixation and the "path" of the fixation object used
        // for eye-tracking setup
        private readonly float _fixationSetupThreshold = 1.0f;
        private readonly float _fixationValidationThreshold = 0.70f;
        private readonly int _fixationMeasurements = 100;
        private GameObject _fixationObject; // Object moved around the screen
        private Vector2 _fixationObjectPosition; // The active unit vector
        private int _fixationObjectPositionIndex = 0;
        private float _updateTimer = 0.0f;
        private readonly float _pathInterval = 1.6f; // Duration of each point being displayed in the path
        private Action _setupCallback; // Optional callback function executed after calibration complete

        /// <summary>
        /// Wrapper function to initialize class and prepare for calibration operations
        /// </summary>
        private void Start()
        {
            // Get the `GazeManager` object
            _presentationManager = FindFirstObjectByType<PresentationManager>();
            if (_presentationManager == null)
            {
                throw new Exception("`GazeManager` object not found in scene");
            }

            // Create moving fixation object
            _fixationObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _fixationObject.name = "calibration_fixation";
            _fixationObject.transform.SetParent(_stimulusAnchor.transform, false);
            _fixationObject.transform.localScale = new Vector3(0.20f, 0.20f, 0.20f);
            _fixationObject.GetComponent<MeshRenderer>().material = new Material(Shader.Find("Sprites/Default"));
            _fixationObject.GetComponent<MeshRenderer>().material.SetColor("_Color", Color.red);
            _fixationObject.SetActive(false);

            // Set initial position of fixation object
            var _fixationObjectPath = _presentationManager.GetFixationObjectPath();
            float _fixationRadius = _presentationManager.GetFixationRadius();
            _fixationObjectPosition = _fixationObjectPath[_fixationObjectPath.Keys.ToList()[_fixationObjectPositionIndex]];
            _fixationObject.transform.localPosition = new Vector3(_fixationObjectPosition.x * _fixationRadius, _fixationObjectPosition.y * _fixationRadius, 0.0f);

            // Setup the calibration prefab instance, initially hidden
            _viewCalibrationPrefabInstance = Instantiate(_viewCalibrationPrefab, _stimulusAnchor.transform);
            _viewCalibrationPrefabInstance.SetActive(false);
        }

        /// <summary>
        /// Public function to externally execute the calibration operations when required
        /// </summary>
        /// <param name="callback">Optional callback function to execute at calibration completion</param>
        public void RunSetup(Action callback = null)
        {
            _isEyeTrackingCalibrationActive = true;
            _fixationObject.SetActive(_isEyeTrackingCalibrationActive);

            // Optional callback function
            _setupCallback = callback;
        }

        /// <summary>
        /// Flash the fixation object 5 times to indicate the start of the validation stage
        /// </summary>
        private IEnumerator FlashFixationObject()
        {
            var renderer = _fixationObject.GetComponent<MeshRenderer>();
            for (int i = 0; i < 5; i++)  // Flash 5 times
            {
                renderer.material.SetColor("_Color", Color.white);
                yield return new WaitForSeconds(0.1f);
                renderer.material.SetColor("_Color", Color.black);
                yield return new WaitForSeconds(0.1f);
            }
            renderer.material.SetColor("_Color", Color.red);  // Reset to original color
        }

        /// <summary>
        /// At the end of the setup stage, begin the validation stage, otherwise end the calibration
        /// </summary>
        private void EndCalibrationStage()
        {
            Debug.Log("Ending calibration stage...");
            if (!_isEyeTrackingCalibrationSetup)
            {
                // Flash the fixation object before starting validation
                StartCoroutine(FlashFixationObject());

                // Completed the setup stage, begin the validation stage
                _isEyeTrackingCalibrationSetup = true;
                _isEyeTrackingCalibrationActive = true;
                Debug.Log("Completed the setup stage, beginning the validation stage...");
            }
            else
            {
                // Completed the validation stage, end the calibration
                Debug.Log("Completed the validation stage, ending the calibration...");
                EndCalibration();
            }
        }

        /// <summary>
        /// At the end of the validation stage, mark the end of the calibration procedure
        /// </summary>
        private void EndCalibration()
        {
            _isEyeTrackingCalibrationActive = false;
            _isEyeTrackingCalibrationComplete = true;
            _fixationObject.SetActive(_isEyeTrackingCalibrationActive);

            // Remove the prefab instance
            Destroy(_viewCalibrationPrefabInstance);

            // Run callback function if specified
            _setupCallback?.Invoke();
        }

        /// <summary>
        /// Get the completion status of the calibration procedure
        /// </summary>
        /// <returns>True if the calibration is complete, false otherwise</returns>
        public bool GetCalibrationComplete() => _isEyeTrackingCalibrationComplete;

        /// <summary>
        /// Get the active status of the calibration procedure
        /// </summary>
        /// <returns>True if the calibration is active, false otherwise</returns>
        public bool GetCalibrationActive() => _isEyeTrackingCalibrationActive;

        /// <summary>
        /// Set the visibility of the calibration prefab instance
        /// </summary>
        /// <param name="state">True to show the prefab, false to hide it</param>
        public void SetViewCalibrationVisibility(bool state) => _viewCalibrationPrefabInstance.SetActive(state);

        /// <summary>
        /// Utility function to capture eye tracking data and store alongside the relevant location
        /// </summary>
        private void RunGazeCapture()
        {
            // Capture eye tracking data and store alongside location
            var l_p = _presentationManager.GetGazeEstimate().GetLeft();
            var r_p = _presentationManager.GetGazeEstimate().GetRight();

            // Determine which data dictionary to use based on the current state
            var gazeData = _isEyeTrackingCalibrationSetup ? _presentationManager.GetValidationData() : _presentationManager.GetSetupData();

            // Test fixation and add to the appropriate data dictionary
            var _fixationObjectPath = _presentationManager.GetFixationObjectPath();
            _presentationManager.SetActiveThreshold(_isEyeTrackingCalibrationSetup ? _fixationValidationThreshold : _fixationSetupThreshold);
            if (_presentationManager.IsFixatedStatic(_fixationObject))
            {
                gazeData[_fixationObjectPath.Keys.ToList()[_fixationObjectPositionIndex]].Add(new(l_p, r_p));
            }

            // If the number of fixations is greater than or equal to 50, proceed to the next position
            if (gazeData[_fixationObjectPath.Keys.ToList()[_fixationObjectPositionIndex]].Count >= _fixationMeasurements)
            {
                Debug.Log("Fixation detected at position \"" + _fixationObjectPath.Keys.ToList()[_fixationObjectPositionIndex] + "\", proceeding to next position...");
                _fixationObject.GetComponent<MeshRenderer>().material.SetColor("_Color", Color.green);
                _fixationCanProceed = true;
            }
        }

        private void Update()
        {
            if (_isEyeTrackingCalibrationActive)
            {
                if (_fixationCanProceed)
                {
                    _updateTimer += Time.deltaTime;
                    if (_updateTimer >= _pathInterval)
                    {
                        // Shift to the next position if the timer has been reached
                        _fixationObjectPositionIndex += 1;
                        var _fixationObjectPath = _presentationManager.GetFixationObjectPath();
                        float _fixationRadius = _presentationManager.GetFixationRadius();
                        if (_fixationObjectPositionIndex > _fixationObjectPath.Count - 1)
                        {
                            _fixationObjectPositionIndex = 0;
                            EndCalibrationStage();
                        }
                        _fixationObjectPosition = _fixationObjectPath[_fixationObjectPath.Keys.ToList()[_fixationObjectPositionIndex]];
                        _fixationObject.transform.localPosition = new Vector3(_fixationObjectPosition.x * _fixationRadius, _fixationObjectPosition.y * _fixationRadius, 0.0f);

                        // Reset the timer, fixation flag, and the color of the fixation object
                        _updateTimer = 0.0f;
                        _fixationCanProceed = false;
                        _fixationObject.GetComponent<MeshRenderer>().material.SetColor("_Color", Color.red);
                    }
                }
                else
                {
                    RunGazeCapture();
                }
            }
        }
    }

}
