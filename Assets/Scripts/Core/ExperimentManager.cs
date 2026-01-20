using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using Headsup.Monitoring;
using UXF;
using MathNet.Numerics.Statistics;

// Custom namespaces
using UI;
using Utilities;

namespace Core
{
    public class ExperimentManager : MonoBehaviour, IHeadsupExperimentManager
    {
        // Loading screen object, parent object that contains all loading screen components
        [SerializeField]
        private GameObject loadingScreen;

        [Header("Operating Modes")]
        [SerializeField]
        private bool demoMode = false;

        [SerializeField]
        private bool debugMode = false;
        private const int DebugBlockSize = 4;

        // Define the types of trials that occur during the experiment timeline
        public enum ETrialType
        {
            Fit = 1,
            Setup = 2,
        };

        // Set the number of trials within a specific block in the experiment timeline
        private enum ETrialCount
        {
            Fit = 1,
            Setup = 1,
        };

        // Define the order of UXF `Blocks` and their expected block numbers (non-zero indexed)
        private enum EBlockSequence
        {
            Fit = 1,
            Setup = 2,
        };

        // List to populate with
        private readonly List<ETrialType> _trainingTimeline = new();
        private readonly List<ETrialType> _mainTimeline = new();

        // Active fields that are updated during trials
        private EBlockSequence _activeBlock; // Store the currently active `EBlockSequence` type
        private ETrialType _activeTrialType; // Store the currently active `ETrialType`
        private PresentationManager.EVisualField _activeVisualField; // Store the currently active `EVisualField`

        // Timing variables
        private float _displayDuration = 0.180f; // 180 milliseconds

        // Store references to Manager classes
        private StimulusManager _stimulusManager;
        private UIManager _uiManager;
        private PresentationManager _presentationManager;
        private SetupManager _setupManager;
        private VRLogger _logger;

        // Input parameters
        private bool _isInputEnabled = false; // Input is accepted
        private bool _isInputReset = true; // Flag to prevent input being held down
        private InputState _lastInputState; // Prior frame input state

        // Signal state from external management tools
        private bool _hasQueuedExit = false;
        private bool _hasQueuedTask = false;
        private bool _hasRunTask = false;
        private bool _hasQueuedCalibration = false;
        private bool _hasRunCalibration = false;

        // System information
        private string _deviceName = "";
        private string _deviceModel = "";
        private float _deviceBattery = 100.0f;

        /// <summary>
        /// Generate the experiment flow
        /// </summary>
        /// <param name="session"></param>
        public void GenerateExperiment(Session session)
        {
            _deviceName = SystemInfo.deviceName;
            _deviceModel = SystemInfo.deviceModel;
            _deviceBattery = SystemInfo.batteryLevel;
            
            // Create a UXF `Block` for each part of the experiment, corresponding to `EBlockSequence` enum
            // Use UXF `Session` to generate experiment timeline from shuffled "Training_" and "Main_" timelines
            session.CreateBlock((int)ETrialCount.Fit); // Pre-experiment headset fit
            session.CreateBlock((int)ETrialCount.Setup); // Pre-experiment setup
            
            // Collect references to other classes
            _stimulusManager = GetComponent<StimulusManager>();
            _uiManager = GetComponent<UIManager>();
            _presentationManager = GetComponent<PresentationManager>();
            _setupManager = GetComponent<SetupManager>();
            _logger = GetComponent<VRLogger>();

            // Update experiment behavior if running in demonstration mode
            if (demoMode)
            {
                Debug.LogWarning("Experiment is being run in Demonstration Mode");

                // Disable fixation requirement
                _presentationManager.SetRequireFixation(false);

                // Update timings
                _displayDuration = 1.80f;
            }

            // Check if debugging is enabled
            if (debugMode)
            {
                // If debugging is enabled, we override a number of options
                Debug.LogWarning("Debug mode has been enabled");

                // Disable fixation
                _presentationManager.SetRequireFixation(false);

                // Update timings
                _displayDuration = 0.50f;

                // Enable the `VRLogger` visibility
                _logger.SetVisible(true);

                // Print the proportions of each `ETrialType` in the training timeline
                Debug.Log(GetTrialProportions(_trainingTimeline));

                // Print the proportions of each `ETrialType` in the main timeline
                Debug.Log(GetTrialProportions(_mainTimeline));

                // Create a message on-screen
                _logger.Log("Debug mode: Enabled");
            }
        }

        /// <summary>
        /// Get the proportions of each `ETrialType` in a timeline
        /// </summary>
        /// <param name="timeline">The timeline to get the proportions of</param>
        /// <returns>A string summary of the proportions of each `ETrialType` in the timeline</returns>
        public string GetTrialProportions(List<ETrialType> timeline)
        {
            var proportions = timeline
                .GroupBy(t => t)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Type = g.Key,
                    Count = g.Count(),
                    Percentage = (double)g.Count() / timeline.Count * 100
                });

            System.Text.StringBuilder summary = new();
            summary.AppendLine($"Timeline Summary (Trials: {timeline.Count})");
            summary.AppendLine("----------------------------------------");

            foreach (var prop in proportions)
            {
                summary.AppendLine(
                    $"{prop.Type}: {prop.Count} trials ({prop.Percentage:F1}%)");
            }

            return summary.ToString();
        }

        /// <summary>
        /// Start the experiment by triggering the next trial
        /// <summary>
        /// Start the experiment by triggering the next trial
        /// </summary>
        /// <param name="session"></param>
        public void BeginExperiment(Session session)
        {
            // If a loading screen was specified, disable / hide it
            if (loadingScreen)
            {
                loadingScreen.SetActive(false);
            }

            // Start the first trial of the Session
            session.BeginNextTrial();
        }

        /// <summary>
        /// Quit the experiment and close the VR application
        /// </summary>
        public void QuitExperiment() => Application.Quit();

        /// <summary>
        /// Get trials of a specific `ETrialType` and `EVisualField` from a `Block`. Used primarily to filter a set of `Trial`s
        /// for calculation of coherence values that are specific to the search parameters.
        /// </summary>
        /// <param name="trialType">`ETrialType` value</param>
        /// <param name="visualField">The active `EVisualField`</param>
        /// <param name="blockIndex">`EBlockSequence` of the `Block` containing the trials</param>
        /// <returns></returns>
        private List<Trial> GetTrialsByType(ETrialType trialType, PresentationManager.EVisualField visualField, EBlockSequence blockIndex)
        {
            List<Trial> result = new();
            var searchBlock = Session.instance.GetBlock((int)blockIndex);
            if (searchBlock.trials.Count > 0)
            {
                foreach (var trial in searchBlock.trials)
                {
                    // Extract results into enum names
                    Enum.TryParse(trial.result["active_visual_field"].ToString(), out PresentationManager.EVisualField priorVisualField);
                    Enum.TryParse(trial.result["trial_type"].ToString(), out ETrialType priorETrialType);
                    if (priorETrialType == trialType && priorVisualField == visualField)
                    {
                        result.Add(trial);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Search within a block to find the index of the previous occurence of that `ETrialType` with a matching active
        /// `EVisualField`.
        /// </summary>
        /// <returns>`int` >= `1` if found, `-1` if no matching `ETrialType` found</returns>
        private int GetPreviousTrialIndex(ETrialType searchType, PresentationManager.EVisualField visualField, int currentIndex)
        {
            if (currentIndex <= 1)
            {
                // Invalid starting index specified
                return -1;
            }

            for (int i = currentIndex - 1; i >= 1; i--)
            {
                var priorTrial = Session.instance.CurrentBlock.GetRelativeTrial(i);
                string priorETrialType = priorTrial.result["trial_type"].ToString();
                Enum.TryParse(priorTrial.result["active_visual_field"].ToString(), out PresentationManager.EVisualField priorVisualField);

                // Compared the stored `name` with the name of the `ETrialType`and the active visual field being searched for
                if (priorETrialType == Enum.GetName(typeof(ETrialType), searchType) && priorVisualField == visualField)
                {
                    // Found `Trial` with matching `ETrialType`
                    return i;
                }
            }

            // No `Trial` found matching the `ETrialType`
            return -1;
        }

        /// <summary>
        /// Store timestamps and locale metadata before presenting the stimuli associated with a Trial.
        /// </summary>
        /// <param name="trial">UXF `Trial` object representing the current trial</param>
        public void RunTrial(Trial trial)
        {
            // Store local date and time data
            Session.instance.CurrentTrial.result["local_date"] = DateTime.Now.ToShortDateString();
            Session.instance.CurrentTrial.result["local_time"] = DateTime.Now.ToShortTimeString();
            Session.instance.CurrentTrial.result["local_timezone"] = TimeZoneInfo.Local.DisplayName;
            Session.instance.CurrentTrial.result["trial_start"] = Time.time;

            // Update the currently active block
            _activeBlock = (EBlockSequence)trial.block.number;

            // Display the active block
            StartCoroutine(DisplayTrial(_activeBlock));
        }

        /// <summary>
        /// Switch-like function presenting a specified stimulus
        /// </summary>
        /// <param name="block">The current block type</param>
        /// <returns></returns>
        private IEnumerator DisplayTrial(EBlockSequence block)
        {
            // Update system status
            _deviceBattery = SystemInfo.batteryLevel;

            // Define the active `ETrialType` depending on the active `Block`
            _activeTrialType = ETrialType.Fit;
            switch (block)
            {
                case EBlockSequence.Fit:
                case EBlockSequence.Setup:
                default:
                    // Default cases
                    break;
            }

            // Debugging information
            Debug.Log("Block: " + block + ", Trial: " + _activeTrialType);

            // Reset all displayed stimuli and UI
            _stimulusManager.SetVisibleAll(false);
            _uiManager.SetVisible(false);

            // Store the current `ETrialType`
            Session.instance.CurrentTrial.result["trial_type"] = Enum.GetName(typeof(ETrialType), _activeTrialType);

            switch (block)
            {
                case EBlockSequence.Fit:
                    _setupManager.SetViewCalibrationVisibility(true);

                    // Input delay
                    yield return StartCoroutine(WaitSeconds(2.0f, true));
                    break;
                case EBlockSequence.Setup:
                    StringBuilder _eyetrackingInstructions = new();
                    _eyetrackingInstructions.Append("A red dot will be visible, and you are to follow the dot movement with your gaze. It will briefly appear green before changing position.\n\n");
                    _eyetrackingInstructions.Append("After a series of movements, the dot will flash before repeating the movements for a second time.\n\n");
                    _eyetrackingInstructions.Append("Notify the facilitator when you are ready to continue.");
                    _uiManager.SetVisible(true);
                    _uiManager.SetHeaderText("Eye-Tracking Setup");
                    _uiManager.SetBodyText(_eyetrackingInstructions.ToString());
                    _uiManager.SetLeftButtonState(false, false, "");
                    _uiManager.SetRightButtonState(false, false, "");

                    // Input delay
                    yield return StartCoroutine(WaitSeconds(0.25f, true));
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Return the value of the currently active Block, stored as `_activeBlock`
        /// </summary>
        /// <returns>String representation of the value</returns>
        public string GetActiveBlock() => _activeBlock.ToString();

        public Dictionary<string, string> GetExperimentStatus() => new()
        {
            { "active_block", _activeBlock.ToString() },
            { "current_trial", Session.instance.currentTrialNum.ToString() },
            { "total_trials", Session.instance.Trials.Count().ToString() },
            { "device_name", _deviceName },
            { "device_model", _deviceModel },
            { "device_battery", _deviceBattery.ToString() }
        };

        public void EndTrial()
        {
            // Store a timestamp and end the trial
            Session.instance.CurrentTrial.result["trial_end"] = Time.time;
            Session.instance.EndCurrentTrial();

            // Reset the active visual field
            _presentationManager.SetActiveField(PresentationManager.EVisualField.Both, false);

            try
            {
                // Proceed to the next trial
                Session.instance.BeginNextTrial();
            }
            catch (NoSuchTrialException)
            {
                // End the experiment session
                Session.instance.End();
            }
        }

        /// <summary>
        /// Function to "force" the end of the experiment, skipping all remaining trials
        /// </summary>
        public void ForceEnd() => _hasQueuedExit = true;

        /// <summary>
        /// Function to start the task, used to signal the start of the task to the Headsup server
        /// </summary>
        public void StartTask() => _hasQueuedTask = true;

        /// <summary>
        /// Function to start the calibration, used to signal the start of the calibration to the Headsup server
        /// </summary>
        public void StartCalibration() => _hasQueuedCalibration = true;

        /// <summary>
        /// Set the input state, `true` allows input, `false` ignores input
        /// </summary>
        /// <param name="state">Input state</param>
        private void SetIsInputEnabled(bool state) => _isInputEnabled = state;

        /// <summary>
        /// Utility function to block further execution until a duration has elapsed
        /// </summary>
        /// <param name="seconds">Duration to wait, measured in seconds</param>
        /// <param name="disableInput">Flag to disable input when `true`</param>
        /// <param name="callback">Function to execute at the end of the duration</param>
        /// <returns></returns>
        private IEnumerator WaitSeconds(float seconds, bool disableInput = false, Action callback = null)
        {
            if (disableInput)
            {
                SetIsInputEnabled(false);
            }

            yield return new WaitForSeconds(seconds);

            if (disableInput)
            {
                SetIsInputEnabled(true);
            }

            // Run callback function
            callback?.Invoke();
        }

        private bool IsSetupScreen() => _activeBlock == EBlockSequence.Setup;

        private bool IsFitScreen() => _activeBlock == EBlockSequence.Fit;

        /// <summary>
        /// Input function to handle `InputState` object and update button presentation or take action depending on
        /// the active `ETrialType`
        /// </summary>
        /// <param name="inputs">`InputState` object</param>
        private void ApplyInputs(InputState inputs)
        {
            // Update the prior input state
            _lastInputState = inputs;
        }

        private void Update()
        {
            // Inputs:
            // - Trigger (any controller): Advance instructions page, (hold) select button
            // - Joystick (any controller): Directional selection of buttons
            if (_isInputEnabled)
            {
                // Reset input state to prevent holding buttons to repeatedly select options
                if (!_isInputReset && !VRInput.AnyInput())
                {
                    _isInputReset = true;
                }
            }

            // Management tools:
            // If the exit signal flag has been set, end the session and quit
            if (_hasQueuedExit)
            {
                Session.instance.End();
                Application.Quit();
            }
            // Handle the start of the task or calibration
            else if (IsFitScreen() && _hasQueuedTask && _isInputReset && !_hasRunTask)
            {
                // Set the flag to prevent the task from being run again
                _hasRunTask = true;

                // Hide the fit calibration screen
                _setupManager.SetViewCalibrationVisibility(false);

                // Trigger controller haptics
                VRInput.SetHaptics(15.0f, 0.4f, 0.1f, true, true);

                EndTrial();
                _isInputReset = false;
            }
            else if (IsSetupScreen() && _hasQueuedCalibration && _isInputReset && !_hasRunCalibration)
            {
                // Set the flag to prevent the calibration from being run again
                _hasRunCalibration = true;

                // Hide the UI
                _uiManager.SetVisible(false);

                // Only provide haptic feedback before calibration is run
                if (!_setupManager.GetCalibrationActive() && !_setupManager.GetCalibrationComplete())
                {
                    // Trigger controller haptics
                    VRInput.SetHaptics(15.0f, 0.4f, 0.1f, true, true);
                }

                // Trigger eye-tracking calibration the end the trial
                _setupManager.RunSetup(() => EndTrial());
                _isInputReset = false;
            }
        }
    }
}
