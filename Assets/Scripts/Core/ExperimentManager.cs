using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using UXF;
using MathNet.Numerics.Statistics;

// Custom namespaces
using UI;
using Utilities;

namespace Core
{
    public class ExperimentManager : MonoBehaviour
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
            InstructionsMotion = 3,
            InstructionsDemoMotion = 4,
            InstructionsSelection = 5,
            InstructionsDemoSelection = 6,
            InstructionsTraining = 7,
            TrainingTrialsBinocular = 8,
            TrainingTrialsMonocularLeft = 9,
            TrainingTrialsMonocularRight = 10,
            TrainingTrialsLateralizedLeft = 11,
            TrainingTrialsLateralizedRight = 12,
            BreakInstructions = 13,
            MainTrialsBinocular = 14,
            MainTrialsMonocularLeft = 15,
            MainTrialsMonocularRight = 16,
            MainTrialsLateralizedLeft = 17,
            MainTrialsLateralizedRight = 18,
            EndInstructions = 19,
        };

        // Set the number of trials within a specific block in the experiment timeline
        private enum ETrialCount
        {
            Fit = 1,
            Setup = 1,
            InstructionsMotion = 1, // Welcome instructions, includes tutorial instructions
            InstructionsDemoMotion = 1,
            InstructionsSelection = 1,
            InstructionsDemoSelection = 1,
            InstructionsTraining = 1,
            TrainingTrialsBinocular = 24, // Training trials, central presentation to both eyes
            TrainingTrialsMonocularLeft = 24, // Training trials, central presentation to left eye
            TrainingTrialsMonocularRight = 24, // Training trials, central presentation to right eye
            TrainingTrialsLateralizedLeft = 24, // Training trials, lateralized presentation to left eye
            TrainingTrialsLateralizedRight = 24, // Training trials, lateralized presentation to right eye
            BreakInstructions = 1,
            MainTrialsBinocular = 24, // Main trials, central presentation to both eyes
            MainTrialsMonocularLeft = 24, // Main trials, central presentation to left eye
            MainTrialsMonocularRight = 24, // Main trials, central presentation to right eye
            MainTrialsLateralizedLeft = 24, // Main trials, lateralized presentation to left eye
            MainTrialsLateralizedRight = 24, // Main trials, lateralized presentation to right eye
            EndInstructions = 1,
        };

        // Define the order of UXF `Blocks` and their expected block numbers (non-zero indexed)
        private enum EBlockSequence
        {
            Fit = 1,
            Setup = 2,
            InstructionsMotion = 3,
            InstructionsDemoMotion = 4,
            InstructionsSelection = 5,
            InstructionsDemoSelection = 6,
            InstructionsTraining = 7,
            Training = 8,
            BreakInstructions = 9,
            Main = 10,
            EndInstructions = 11
        };

        // List to populate with
        private readonly List<ETrialType> _trainingTimeline = new();
        private readonly List<ETrialType> _mainTimeline = new();

        // Active fields that are updated during trials
        private EBlockSequence _activeBlock; // Store the currently active `EBlockSequence` type
        private ETrialType _activeTrialType; // Store the currently active `ETrialType`
        private PresentationManager.EVisualField _activeVisualField; // Store the currently active `EVisualField`
        private float _activeCoherence; // Current coherence value

        // "Training_"-type coherence values start at `0.2f` and are adjusted
        private float _trainingBinocularCoherence = 0.2f; // Unified across left and right eye
        private float _trainingMonocularCoherenceLeft = 0.2f;
        private float _trainingMonocularCoherenceRight = 0.2f;
        private float _trainingLateralizedCoherenceLeft = 0.2f;
        private float _trainingLateralizedCoherenceRight = 0.2f;

        // "Main_"-type coherence values are calculated from "Training_"-type coherence values
        private Tuple<float, float> _mainBinocularCoherence; // Unified across left and right eye
        private Tuple<float, float> _mainMonocularCoherenceLeft;
        private Tuple<float, float> _mainMonocularCoherenceRight;
        private Tuple<float, float> _mainLateralizedCoherenceLeft;
        private Tuple<float, float> _mainLateralizedCoherenceRight;

        // Timing variables
        private readonly float _preDisplayDuration = 0.5f; // 500 milliseconds
        private readonly float _postDisplayDuration = 0.1f; // 100 milliseconds
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

        // Input button slider GameObjects and variables
        private readonly float _triggerThreshold = 0.8f;
        private readonly float _joystickThreshold = 0.6f;
        private readonly float _buttonSlideThreshold = 0.99f;
        private readonly float _buttonHoldFactor = 2.0f;

        // Selected button state
        private int _selectedButtonIndex = 1; // Starting index of 1, actual range [0, 3]
        private bool _hasMovedSelection = false; // Initially `false` until first movement made

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

            // Generate the experiment timeline
            // Generate the "Training_"-type trial timeline and shuffle
            Dictionary<ETrialType, ETrialCount> trainingTrialMapping = new()
            {
                { ETrialType.TrainingTrialsBinocular, ETrialCount.TrainingTrialsBinocular },
                { ETrialType.TrainingTrialsMonocularLeft, ETrialCount.TrainingTrialsMonocularLeft },
                { ETrialType.TrainingTrialsMonocularRight, ETrialCount.TrainingTrialsMonocularRight },
                { ETrialType.TrainingTrialsLateralizedLeft, ETrialCount.TrainingTrialsLateralizedLeft },
                { ETrialType.TrainingTrialsLateralizedRight, ETrialCount.TrainingTrialsLateralizedRight }
            };

            foreach (var (type, count) in trainingTrialMapping)
            {
                int _trialCount = (int)count;
                if (debugMode)
                {
                    // If debug mode is enabled, use smaller blocks
                    _trialCount = DebugBlockSize;
                }

                for (int i = 0; i < _trialCount; i++)
                {
                    _trainingTimeline.Add(type);
                }
            }
            _trainingTimeline.Shuffle();

            // Generate the "Main_"-type trial timeline and shuffle
            Dictionary<ETrialType, ETrialCount> mainTrialMapping = new()
            {
                { ETrialType.MainTrialsBinocular, ETrialCount.MainTrialsBinocular },
                { ETrialType.MainTrialsMonocularLeft, ETrialCount.MainTrialsMonocularLeft },
                { ETrialType.MainTrialsMonocularRight, ETrialCount.MainTrialsMonocularRight },
                { ETrialType.MainTrialsLateralizedLeft, ETrialCount.MainTrialsLateralizedLeft },
                { ETrialType.MainTrialsLateralizedRight, ETrialCount.MainTrialsLateralizedRight }
            };

            foreach (var (type, count) in mainTrialMapping)
            {
                int _trialCount = (int)count;
                if (debugMode)
                {
                    // If debug mode is enabled, use smaller blocks
                    _trialCount = DebugBlockSize;
                }

                for (int i = 0; i < _trialCount; i++)
                {
                    _mainTimeline.Add(type);
                }
            }
            _mainTimeline.Shuffle();

            // Create a UXF `Block` for each part of the experiment, corresponding to `EBlockSequence` enum
            // Use UXF `Session` to generate experiment timeline from shuffled "Training_" and "Main_" timelines
            session.CreateBlock((int)ETrialCount.Fit); // Pre-experiment headset fit
            session.CreateBlock((int)ETrialCount.Setup); // Pre-experiment setup
            session.CreateBlock((int)ETrialCount.InstructionsMotion); // Pre-experiment instructions
            session.CreateBlock((int)ETrialCount.InstructionsDemoMotion); // Pre-experiment motion demo
            session.CreateBlock((int)ETrialCount.InstructionsSelection); // Pre-experiment selection instructions
            session.CreateBlock((int)ETrialCount.InstructionsDemoSelection); // Pre-experiment selection demo
            session.CreateBlock((int)ETrialCount.InstructionsTraining); // Pre-experiment training instructions
            session.CreateBlock(_trainingTimeline.Count); // Training trials
            session.CreateBlock((int)ETrialCount.BreakInstructions); // Mid-experiment instructions
            session.CreateBlock(_mainTimeline.Count); // Main trials
            session.CreateBlock((int)ETrialCount.EndInstructions); // Post-experiment instructions

            // Collect references to other classes
            _stimulusManager = GetComponent<StimulusManager>();
            _uiManager = GetComponent<UIManager>();
            _presentationManager = GetComponent<PresentationManager>();
            _setupManager = GetComponent<SetupManager>();
            _logger = GetComponent<VRLogger>();

            // Update the PresentationManager value for the aperture offset to be the stimulus radius
            _presentationManager.SetStimulusWidth(_stimulusManager.GetApertureWidth());

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
        /// Update the coherence value depending on the accuracy of the given response to the current `Trial` and the
        /// previous `Trial`. This function is only executed during the "Training_"-type trials.
        /// </summary>
        private void UpdateCoherences()
        {
            var targetTrialType = _activeTrialType;
            float coherenceDelta = 0.0f;

            // Current `Trial` information
            var currentTrial = Session.instance.CurrentTrial;
            bool currentTrialAccuracy = (bool)currentTrial.result["correct_selection"];
            int currentTrialIndex = currentTrial.numberInBlock;
            float currentTrialCoherence = (float)currentTrial.result["active_coherence"];
            if (currentTrialAccuracy)
            {
                // Search for previous trial of matching `ETrialType` for comparison
                int previousTrialIndex = GetPreviousTrialIndex(_activeTrialType, _activeVisualField, currentTrialIndex);
                if (previousTrialIndex != -1)
                {
                    // Prior trial found matching the current `ETrialType`
                    var previousTrial = Session.instance.CurrentBlock.GetRelativeTrial(previousTrialIndex);
                    bool previousTrialAccuracy = (bool)previousTrial.result["correct_selection"];
                    float previousTrialCoherence = (float)previousTrial.result["active_coherence"];

                    // Check criteria for two consecutive correct responses
                    if (previousTrialAccuracy && currentTrialCoherence == previousTrialCoherence)
                    {
                        // Reduce the target `ETrialType` coherence value, increasing difficulty
                        coherenceDelta = -0.01f;

                        if (debugMode)
                        {
                            _logger.Log("Coherence: decreased");
                        }
                    }
                }
            }
            else
            {
                // Increase the coherence value, reducing difficulty
                coherenceDelta = 0.01f;

                if (debugMode)
                {
                    _logger.Log("Coherence: increased");
                }
            }

            // Apply the modification to the `ETrialType` coherence value
            if (targetTrialType == ETrialType.TrainingTrialsBinocular)
            {
                _trainingBinocularCoherence += coherenceDelta;
            }
            else if (targetTrialType is ETrialType.TrainingTrialsMonocularLeft or ETrialType.TrainingTrialsMonocularRight)
            {
                if (_activeVisualField == PresentationManager.EVisualField.Left)
                {
                    _trainingMonocularCoherenceLeft += coherenceDelta;
                }
                else
                {
                    _trainingMonocularCoherenceRight += coherenceDelta;
                }
            }
            else if (targetTrialType is ETrialType.TrainingTrialsLateralizedLeft or ETrialType.TrainingTrialsLateralizedRight)
            {
                if (_activeVisualField == PresentationManager.EVisualField.Left)
                {
                    _trainingLateralizedCoherenceLeft += coherenceDelta;
                }
                else
                {
                    _trainingLateralizedCoherenceRight += coherenceDelta;
                }
            }
        }

        /// <summary>
        /// Helper function to calculate the low and high coherence pairs from the median value of a set of 20
        /// coherence values. Requires a set of trials, and the name of the field as stored in the `results` dictionary.
        /// </summary>
        /// <param name="trialSet">Set of `Trial` objects</param>
        /// <param name="coherenceFieldName">Field name storing the coherence value</param>
        /// <returns></returns>
        private Tuple<float, float> CalculateCoherencePairs(List<Trial> trialSet, string coherenceFieldName)
        {
            List<float> coherences = trialSet.Select(trial => (float)trial.result[coherenceFieldName]).ToList();
            coherences.Reverse();

            // Restrict `kMed` value [0.12f, 0.5f]
            float kMed = coherences.Take(20).Median();
            kMed = kMed < 0.12f ? 0.12f : kMed;
            kMed = kMed > 0.5f ? 0.5f : kMed;

            return new Tuple<float, float>(0.5f * kMed, 2.0f * kMed);
        }

        /// <summary>
        /// Utility function to calculate coherence values prior to "Main_"-type trials
        /// </summary>
        private void CalculateCoherences()
        {
            // Calculate coherences for `TrainingTrialsBinocular` trials
            var binocularTrials = GetTrialsByType(ETrialType.TrainingTrialsBinocular, PresentationManager.EVisualField.Both, EBlockSequence.Training);
            _mainBinocularCoherence = CalculateCoherencePairs(binocularTrials, "training_binocular_coherence");

            // Calculate coherences for left `Training_Trials_Monocular` trials
            var monocularTrialsLeft = GetTrialsByType(ETrialType.TrainingTrialsMonocularLeft, PresentationManager.EVisualField.Left, EBlockSequence.Training);
            _mainMonocularCoherenceLeft = CalculateCoherencePairs(monocularTrialsLeft, "training_monocular_coherence_left");

            // Calculate coherences for right `Training_Trials_Monocular` trials
            var monocularTrialsRight = GetTrialsByType(ETrialType.TrainingTrialsMonocularRight, PresentationManager.EVisualField.Right, EBlockSequence.Training);
            _mainMonocularCoherenceRight = CalculateCoherencePairs(monocularTrialsRight, "training_monocular_coherence_right");

            // Calculate coherences for left `Training_Trials_Lateralized` trials
            var lateralizedTrialsLeft = GetTrialsByType(ETrialType.TrainingTrialsLateralizedLeft, PresentationManager.EVisualField.Left, EBlockSequence.Training);
            _mainLateralizedCoherenceLeft = CalculateCoherencePairs(lateralizedTrialsLeft, "training_lateralized_coherence_left");

            // Calculate coherences for right `Training_Trials_Lateralized` trials
            var lateralizedTrialsRight = GetTrialsByType(ETrialType.TrainingTrialsLateralizedRight, PresentationManager.EVisualField.Right, EBlockSequence.Training);
            _mainLateralizedCoherenceRight = CalculateCoherencePairs(lateralizedTrialsRight, "training_lateralized_coherence_right");
        }

        /// <summary>
        /// Setup the "Instructions" screens presented to participants
        /// </summary>
        private void SetupInstructions()
        {
            // Setup the UI manager with instructions
            List<string> Instructions = new();

            // Configure the list of instructions depending on the instruction block
            if (_activeBlock == EBlockSequence.InstructionsMotion)
            {
                // Instructions shown to the participant before the start of the experiment
                Instructions.AddRange(new List<string>{
                    "During this task, you will be presented with a field of moving dots and a fixation cross. The dot movement will be visible for only a short period of time and will appear either around the cross or next to the cross.\n\n\nPress the <b>Trigger</b> to select <b>Next</b> and continue.",
                    "During the trials, you <b>must maintain focus on the central fixation cross</b> whenever it is visible and during dot motion. The dots will be visible in either one eye or both eyes at once.\n\nSome of the dots will move only up or only down, and the rest of the dots will move randomly as a distraction.\n\n\nPress the <b>Trigger</b> to select <b>Continue</b> and preview the dot movement.",
                });
            }
            else if (_activeBlock == EBlockSequence.InstructionsSelection)
            {
                // Instructions shown to the participant before the start of the experiment
                Instructions.AddRange(new List<string>{
                    "After viewing the dot movement, you will be asked if you thought the dots moving together moved up or down.\n\nYou will have four options to choose from:\n<b>Up - Very Confident\t\t\t</b>\n<b>Up - Somewhat Confident\t\t</b>\n<b>Down - Somewhat Confident\t</b>\n<b>Down - Very Confident\t\t</b>\n\nPress the <b>Trigger</b> to select <b>Next</b> and continue.",
                    "You <b>must</b> select one of the four options. Please choose the one that best represents your conclusion about how the dots were moving and your confidence in that conclusion.\n\nUse the <b>Joystick</b> to move the cursor between options, and hold the <b>Trigger</b> for approximately 1 second to select an option.\n\n\nPress the <b>Trigger</b> to select <b>Continue</b> and practice making a selection.",
                });
            }
            else if (_activeBlock == EBlockSequence.InstructionsTraining)
            {
                // Instructions shown to the participant before the start of the experiment
                Instructions.AddRange(new List<string>{
                    "You will first play <b>" + _trainingTimeline.Count + " training trials</b>. After the training trials, you will be shown an instruction screen before continuing to the main trials.\n\nYou are about to start the training trials.\n\n\nWhen you are ready and comfortable, press the <b>Trigger</b> to select <b>Continue</b> and begin.",
                });
            }
            else if (_activeBlock == EBlockSequence.BreakInstructions)
            {
                // Instructions shown to the participant between the training trials and the main trials
                Instructions.AddRange(new List<string>{
                    "That concludes all the training trials. You will now play <b>" + _mainTimeline.Count + " main trials</b>.\n\nWhen you are ready and comfortable, press the <b>Trigger</b> to select <b>Continue</b> and start the main trials.",
                });
            }
            else if (_activeBlock == EBlockSequence.EndInstructions)
            {
                // Instructions shown to the participant after they have completed all the trials
                Instructions.AddRange(new List<string>{
                    "That concludes all the trials for this task. Please notify the experiment facilitator, and you can remove the headset carefully after releasing the rear adjustment wheel.",
                });
            }

            // Enable pagination if > 1 page of instructions, then set the active instructions
            _uiManager.SetPages(Instructions);
        }

        /// <summary>
        /// Setup operations to perform prior to any motion stimuli. Also responsible for generating coherence values
        /// after the calibration stages.
        /// </summary>
        private void SetupMotion(ETrialType trial)
        {
            // Step 1: Setup the camera according to the active `ETrialType`
            if (trial is ETrialType.TrainingTrialsBinocular or ETrialType.MainTrialsBinocular)
            {
                // Activate both cameras in binocular mode
                _presentationManager.SetActiveField(PresentationManager.EVisualField.Both, false);
            }
            else if (trial is ETrialType.TrainingTrialsMonocularLeft or ETrialType.MainTrialsMonocularLeft)
            {
                // Activate one camera in monocular mode, without lateralization
                _presentationManager.SetActiveField(PresentationManager.EVisualField.Left, false);
            }
            else if (trial is ETrialType.TrainingTrialsMonocularRight or ETrialType.MainTrialsMonocularRight)
            {
                // Activate one camera in monocular mode, without lateralization
                _presentationManager.SetActiveField(PresentationManager.EVisualField.Right, false);
            }
            else if (trial is ETrialType.TrainingTrialsLateralizedLeft or ETrialType.MainTrialsLateralizedLeft)
            {
                // Activate one camera in monocular mode, with lateralization
                _presentationManager.SetActiveField(PresentationManager.EVisualField.Left, true);
            }
            else if (trial is ETrialType.TrainingTrialsLateralizedRight or ETrialType.MainTrialsLateralizedRight)
            {
                // Activate one camera in monocular mode, with lateralization
                _presentationManager.SetActiveField(PresentationManager.EVisualField.Right, true);
            }

            // Step 2: Set the coherence value depending on the `ETrialType`
            _activeVisualField = _presentationManager.GetActiveField();
            if (trial == ETrialType.TrainingTrialsBinocular)
            {
                _activeCoherence = _trainingBinocularCoherence;
            }
            else if (trial is ETrialType.TrainingTrialsMonocularLeft or ETrialType.TrainingTrialsMonocularRight)
            {
                // Select the coherence value for the active monocular training trial
                _activeCoherence = _activeVisualField == PresentationManager.EVisualField.Left ? _trainingMonocularCoherenceLeft : _trainingMonocularCoherenceRight;
            }
            else if (trial is ETrialType.TrainingTrialsLateralizedLeft or ETrialType.TrainingTrialsLateralizedRight)
            {
                // Select the coherence value for the active lateralized training trial
                _activeCoherence = _activeVisualField == PresentationManager.EVisualField.Left ? _trainingLateralizedCoherenceLeft : _trainingLateralizedCoherenceRight;
            }
            // All "Main_"-type coherence selections include a difficulty selection
            else if (trial == ETrialType.MainTrialsBinocular)
            {
                _activeCoherence = UnityEngine.Random.value > 0.5 ? _mainBinocularCoherence.Item1 : _mainBinocularCoherence.Item2;

                // Store if the high or low coherence was selected
                Session.instance.CurrentTrial.result["main_binocular_coherence_type"] = _activeCoherence == _mainBinocularCoherence.Item1 ? "low" : "high";
                Debug.Log("Coherence type: " + Session.instance.CurrentTrial.result["main_binocular_coherence_type"]);
            }
            else if (trial is ETrialType.MainTrialsMonocularLeft or ETrialType.MainTrialsMonocularRight)
            {
                // Select the appropriate coherence pair (left or right), then select a coherence value (low or high)
                var CoherencePair = _activeVisualField == PresentationManager.EVisualField.Left ? _mainMonocularCoherenceLeft : _mainMonocularCoherenceRight;
                _activeCoherence = UnityEngine.Random.value > 0.5 ? CoherencePair.Item1 : CoherencePair.Item2;

                // Store if the high or low coherence was selected
                Session.instance.CurrentTrial.result["main_monocular_coherence_type"] = _activeCoherence == CoherencePair.Item1 ? "low" : "high";
                Debug.Log("Coherence type: " + Session.instance.CurrentTrial.result["main_monocular_coherence_type"]);
            }
            else if (trial is ETrialType.MainTrialsLateralizedLeft or ETrialType.MainTrialsLateralizedRight)
            {
                // Select the appropriate coherence pair (left or right), then select a coherence value (low or high)
                var CoherencePair = _activeVisualField == PresentationManager.EVisualField.Left ? _mainLateralizedCoherenceLeft : _mainLateralizedCoherenceRight;
                _activeCoherence = UnityEngine.Random.value > 0.5 ? CoherencePair.Item1 : CoherencePair.Item2;

                // Store if the high or low coherence was selected
                Session.instance.CurrentTrial.result["main_lateralized_coherence_type"] = _activeCoherence == CoherencePair.Item1 ? "low" : "high";
                Debug.Log("Coherence type: " + Session.instance.CurrentTrial.result["main_lateralized_coherence_type"]);
            }

            // Apply coherence value (RDK-70)
            _stimulusManager.SetCoherence(_activeCoherence);
            Debug.Log("Active coherence: " + _stimulusManager.GetCoherence());

            // Set the reference direction and re-randomize distractor dot motion
            float dotDirection = UnityEngine.Random.value > 0.5f ? Mathf.PI / 2 : Mathf.PI * 3 / 2;
            _stimulusManager.SetDirection(dotDirection);

            // Store motion-related data points
            Session.instance.CurrentTrial.result["dot_direction"] = dotDirection == Mathf.PI / 2 ? "up" : "down";
            Session.instance.CurrentTrial.result["active_coherence"] = _activeCoherence;
            if (_activeBlock == EBlockSequence.Training)
            {
                Session.instance.CurrentTrial.result["training_binocular_coherence"] = _trainingBinocularCoherence;
                Session.instance.CurrentTrial.result["training_monocular_coherence_left"] = _trainingMonocularCoherenceLeft;
                Session.instance.CurrentTrial.result["training_monocular_coherence_right"] = _trainingMonocularCoherenceRight;
                Session.instance.CurrentTrial.result["training_lateralized_coherence_left"] = _trainingLateralizedCoherenceLeft;
                Session.instance.CurrentTrial.result["training_lateralized_coherence_right"] = _trainingLateralizedCoherenceRight;
            }
            if (_activeBlock == EBlockSequence.Main)
            {
                Session.instance.CurrentTrial.result["main_binocular_coherence"] = _mainBinocularCoherence.Item1 + "," + _mainBinocularCoherence.Item2;
                Session.instance.CurrentTrial.result["main_monocular_coherence_left"] = _mainMonocularCoherenceLeft.Item1 + "," + _mainMonocularCoherenceLeft.Item2;
                Session.instance.CurrentTrial.result["main_monocular_coherence_right"] = _mainMonocularCoherenceRight.Item1 + "," + _mainMonocularCoherenceRight.Item2;
                Session.instance.CurrentTrial.result["main_lateralized_coherence_left"] = _mainLateralizedCoherenceLeft.Item1 + "," + _mainLateralizedCoherenceLeft.Item2;
                Session.instance.CurrentTrial.result["main_lateralized_coherence_right"] = _mainLateralizedCoherenceRight.Item1 + "," + _mainLateralizedCoherenceRight.Item2;
            }
            Session.instance.CurrentTrial.result["active_visual_field"] = _activeVisualField.ToString();
            Session.instance.CurrentTrial.result["motion_duration"] = _displayDuration;
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

            // Get the relative position of the current trial in the block
            int RelativeTrialNumber = Session.instance.CurrentTrial.numberInBlock - 1;

            // Define the active `ETrialType` depending on the active `Block`
            _activeTrialType = ETrialType.InstructionsMotion;
            switch (block)
            {
                case EBlockSequence.Training:
                    // Set the `_activeTrialType` depending on the position within the `_trainingTimeline`
                    _activeTrialType = _trainingTimeline[RelativeTrialNumber];
                    break;
                case EBlockSequence.InstructionsDemoMotion:
                    // Set the `_activeTrialType` depending on the position within the `_trainingTimeline`
                    _activeTrialType = ETrialType.InstructionsDemoMotion;
                    break;
                case EBlockSequence.InstructionsDemoSelection:
                    // Set the `_activeTrialType` depending on the position within the `_trainingTimeline`
                    _activeTrialType = ETrialType.InstructionsDemoSelection;
                    break;
                case EBlockSequence.BreakInstructions:
                    _activeTrialType = ETrialType.BreakInstructions;
                    break;
                case EBlockSequence.Main:
                    // Set the `_activeTrialType` depending on the position within the `_mainTimeline`
                    _activeTrialType = _mainTimeline[RelativeTrialNumber];
                    break;
                case EBlockSequence.EndInstructions:
                    _activeTrialType = ETrialType.EndInstructions;
                    break;
                case EBlockSequence.Fit:
                case EBlockSequence.Setup:
                case EBlockSequence.InstructionsMotion:
                case EBlockSequence.InstructionsSelection:
                case EBlockSequence.InstructionsTraining:
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
                case EBlockSequence.InstructionsMotion:
                    SetupInstructions();
                    _uiManager.SetVisible(true);
                    _uiManager.SetHeaderText("Instructions");
                    _uiManager.SetLeftButtonState(false, false, "Back");
                    _uiManager.SetRightButtonState(true, true, "Next");

                    // Input delay
                    yield return StartCoroutine(WaitSeconds(0.25f, true));
                    break;
                case EBlockSequence.InstructionsDemoMotion:
                    // Run setup for the motion stimuli
                    SetupMotion(ETrialType.TrainingTrialsBinocular);
                    yield return StartCoroutine(DisplayMotion(3.0f));
                    EndTrial();
                    break;
                case EBlockSequence.InstructionsSelection:
                    SetupInstructions();
                    _uiManager.SetVisible(true);
                    _uiManager.SetHeaderText("Instructions");
                    _uiManager.SetLeftButtonState(false, false, "Back");
                    _uiManager.SetRightButtonState(true, true, "Next");

                    // Input delay
                    yield return StartCoroutine(WaitSeconds(0.25f, true));
                    break;
                case EBlockSequence.InstructionsDemoSelection:
                    // Run setup for the decision stimuli
                    SetupMotion(ETrialType.TrainingTrialsBinocular);
                    yield return StartCoroutine(DisplaySelection());
                    break;
                case EBlockSequence.InstructionsTraining:
                    SetupInstructions();
                    _uiManager.SetVisible(true);
                    _uiManager.SetHeaderText("Instructions");
                    _uiManager.SetLeftButtonState(false, false, "Back");
                    _uiManager.SetRightButtonState(true, true, "Continue");

                    // Input delay
                    yield return StartCoroutine(WaitSeconds(0.25f, true));
                    break;
                case EBlockSequence.Training:
                case EBlockSequence.Main:
                    // Run setup for the motion stimuli
                    SetupMotion(_activeTrialType);
                    yield return StartCoroutine(DisplayMotion(_displayDuration));
                    yield return StartCoroutine(DisplaySelection());
                    break;
                case EBlockSequence.BreakInstructions:
                    SetupInstructions();

                    // Run calibration calculations
                    CalculateCoherences();

                    // Override and set the camera to display in both eyes
                    _presentationManager.SetActiveField(PresentationManager.EVisualField.Both);

                    _uiManager.SetVisible(true);
                    _uiManager.SetHeaderText("Main Trials");
                    _uiManager.SetLeftButtonState(false, false, "Back");
                    _uiManager.SetRightButtonState(true, true, "Continue");

                    // Input delay
                    yield return StartCoroutine(WaitSeconds(0.15f, true));
                    break;
                case EBlockSequence.EndInstructions:
                    SetupInstructions();
                    // Override and set the camera to display in both eyes
                    _presentationManager.SetActiveField(PresentationManager.EVisualField.Both);

                    _uiManager.SetVisible(true);
                    _uiManager.SetHeaderText("Complete");
                    _uiManager.SetLeftButtonState(false, false, "Back");
                    _uiManager.SetRightButtonState(true, true, "Finish");

                    // Input delay
                    yield return StartCoroutine(WaitSeconds(1.0f, true));
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

        /// <summary>
        /// Utility function to display "Feedback_"-type stimuli
        /// </summary>
        /// <param name="correct">`true` to display green cross, `false` to display red cross</param>
        // private IEnumerator DisplayFeedback(bool correct)
        // {
        //     _stimulusManager.SetFixationCrossColor(correct ? "green" : "red");
        //     _stimulusManager.SetFixationCrossVisibility(true);
        //     _stimulusManager.SetVisible(correct ? EStimulusType.Feedback_Correct : EStimulusType.Feedback_Incorrect, true);
        //     yield return StartCoroutine(WaitSeconds(1.0f, true));
        //     _stimulusManager.SetVisible(correct ? EStimulusType.Feedback_Correct : EStimulusType.Feedback_Incorrect, false);
        //     _stimulusManager.SetFixationCrossVisibility(false);
        //     _stimulusManager.SetFixationCrossColor("white");
        // }

        /// <summary>
        /// Utility function to display the dot motion stimulus and wait for a response, used in all trial types
        /// </summary>
        /// <returns></returns>
        private IEnumerator DisplayMotion(float duration)
        {
            // Disable input and wait for fixation if enabled
            SetIsInputEnabled(false);
            _stimulusManager.SetFixationCrossVisibility(true);
            if (_presentationManager.GetRequireFixation())
            {
                Debug.Log("Waiting for fixation...");
                yield return StartCoroutine(_presentationManager.IsFixatedDuration(_stimulusManager.GetFixationAnchor(), 0.5f));
                Debug.Log("Fixated, continuing...");
            }
            else
            {
                Debug.Log("Fixation not required");
            }
            yield return StartCoroutine(WaitSeconds(_postDisplayDuration, true));

            // Present fixation stimulus
            _stimulusManager.SetVisible(EStimulusType.Fixation, true);

            // Wait either for fixation or a fixed duration if fixation not required
            if (_presentationManager.GetRequireFixation())
            {
                Debug.Log("Waiting for fixation...");
                yield return StartCoroutine(_presentationManager.IsFixatedDuration(_stimulusManager.GetFixationAnchor(), 0.5f));
                Debug.Log("Fixated, continuing...");
            }
            else
            {
                Debug.Log("Fixation not required");
                yield return StartCoroutine(WaitSeconds(_preDisplayDuration, true));
            }
            _stimulusManager.SetVisible(EStimulusType.Fixation, false);

            // Present motion stimulus
            _stimulusManager.SetVisible(EStimulusType.Motion, true);
            yield return StartCoroutine(WaitSeconds(duration, true));
            _stimulusManager.SetVisible(EStimulusType.Motion, false);
            _stimulusManager.SetFixationCrossVisibility(false);
        }

        private IEnumerator DisplaySelection()
        {
            // Present decision stimulus and wait for response
            Session.instance.CurrentTrial.result["decision_start"] = Time.time;
            _stimulusManager.ResetCursor();
            _stimulusManager.SetCursorSide(_activeVisualField == PresentationManager.EVisualField.Left ? StimulusManager.ECursorSide.Right : StimulusManager.ECursorSide.Left);
            _stimulusManager.SetCursorVisiblity(true);
            _stimulusManager.SetVisible(EStimulusType.Decision, true);
            yield return StartCoroutine(WaitSeconds(0.1f, true));
        }

        /// <summary>
        /// Wrapper function to handle selection of a specific direction and confidence value
        /// </summary>
        private void HandleSelection(string selection)
        {
            // Store timing data
            Session.instance.CurrentTrial.result["decision_end"] = Time.time;
            Session.instance.CurrentTrial.result["decision_rt"] = (float)Session.instance.CurrentTrial.result["decision_end"] - (float)Session.instance.CurrentTrial.result["decision_start"];

            // Store the selection value
            Session.instance.CurrentTrial.result["selected_direction"] = selection;

            // Determine if a correct response was made
            Session.instance.CurrentTrial.result["correct_selection"] = false;
            if ((selection == "vc_u" || selection == "sc_u") && _stimulusManager.GetDirection() == (float)Math.PI / 2)
            {
                Session.instance.CurrentTrial.result["correct_selection"] = true;
            }
            else if ((selection == "vc_d" || selection == "sc_d") && _stimulusManager.GetDirection() == (float)Math.PI * 3 / 2)
            {
                Session.instance.CurrentTrial.result["correct_selection"] = true;
            }

            // If currently in a "Training_"-type block, update the coherences after the selection has been handled
            if (_activeBlock == EBlockSequence.Training)
            {
                UpdateCoherences();
            }

            // Reset the button and UI states and end the current `Trial`
            _isInputReset = false;
            _hasMovedSelection = false;
            _stimulusManager.SetCursorVisiblity(false);
            ResetButtons();
            EndTrial();
        }

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

        // Functions to bulk-classify ETrialType values
        private bool IsInstructionsScreen() => _activeBlock is EBlockSequence.InstructionsMotion or
            EBlockSequence.InstructionsSelection or
            EBlockSequence.InstructionsTraining or
            EBlockSequence.BreakInstructions or
            EBlockSequence.EndInstructions;

        private bool IsStimulusScreen() => _activeBlock is EBlockSequence.InstructionsDemoMotion or
            EBlockSequence.InstructionsDemoSelection or
            EBlockSequence.Training or
            EBlockSequence.Main;

        private bool IsSetupScreen() => _activeBlock == EBlockSequence.Setup;

        private bool IsFitScreen() => _activeBlock == EBlockSequence.Fit;

        /// <summary>
        /// Input function to handle `InputState` object and update button presentation or take action depending on
        /// the active `ETrialType`
        /// </summary>
        /// <param name="inputs">`InputState` object</param>
        private void ApplyInputs(InputState inputs)
        {
            var buttonControllers = _stimulusManager.GetButtonSliders();

            // Increment button selection
            if (!(VRInput.LeftTrigger() || VRInput.RightTrigger()) && (inputs.LeftJoystick.y > _joystickThreshold || inputs.RightJoystick.y > _joystickThreshold) && _isInputReset)
            {
                DecrementButtonSelection();
                _isInputReset = false;

                // Update the cursor position
                _stimulusManager.SetCursorIndex(_selectedButtonIndex);
            }

            // Decrement button selection
            if (!(VRInput.LeftTrigger() || VRInput.RightTrigger()) && (inputs.LeftJoystick.y < -_joystickThreshold || inputs.RightJoystick.y < -_joystickThreshold) && _isInputReset)
            {
                IncrementButtonSelection();
                _isInputReset = false;

                // Update the cursor position
                _stimulusManager.SetCursorIndex(_selectedButtonIndex);
            }

            // Apply trigger input if trigger inputs are active and a selection has been made
            if ((VRInput.LeftTrigger() || VRInput.RightTrigger()) && _hasMovedSelection)
            {
                buttonControllers[_selectedButtonIndex].SetSliderValue(buttonControllers[_selectedButtonIndex].GetSliderValue() + (_buttonHoldFactor * Time.deltaTime));

                // Signify that these inputs are new, store `last_keypress_start` timestamp
                if ((_lastInputState.LeftTrigger < _triggerThreshold && VRInput.LeftTrigger()) ||
                    (_lastInputState.RightTrigger < _triggerThreshold && VRInput.RightTrigger()))
                {
                    Session.instance.CurrentTrial.result["last_keypress_start"] = Time.time;
                }
            }

            // Audit button state and handle a button selection if the button has been selected for the required duration
            if (buttonControllers[_selectedButtonIndex].GetSliderValue() >= _buttonSlideThreshold)
            {
                // Provide haptic feedback
                VRInput.SetHaptics(15.0f, 0.4f, 0.1f, true, true);
                Session.instance.CurrentTrial.result["last_keypress_end"] = Time.time;

                // Store selected button response
                switch (_selectedButtonIndex)
                {
                    case 0:
                        HandleSelection("vc_u");
                        break;
                    case 1:
                        HandleSelection("sc_u");
                        break;
                    case 2:
                        HandleSelection("sc_d");
                        break;
                    case 3:
                        HandleSelection("vc_d");
                        break;
                    default:
                        break;
                }

                // Reset the slider value of the selected button
                buttonControllers[_selectedButtonIndex].SetSliderValue(0.0f);
            }

            // Update the prior input state
            _lastInputState = inputs;
        }

        /// <summary>
        /// Utility function to reset the state of all `ButtonSliderFill` buttons
        /// </summary>
        private void ResetButtons()
        {
            foreach (var button in _stimulusManager.GetButtonSliders())
            {
                button.SetSliderValue(0.0f);
            }
        }

        /// <summary>
        /// Increment the selected button, based on input from the controller joystick
        /// </summary>
        private void IncrementButtonSelection()
        {
            if (!_hasMovedSelection)
            {
                // Initial button press
                _selectedButtonIndex = 2;
                _hasMovedSelection = true;
            }
            else if (_selectedButtonIndex < 3)
            {
                _selectedButtonIndex += 1;
            }
        }

        /// <summary>
        /// Decrement the selected button, based on input from the controller joystick
        /// </summary>
        private void DecrementButtonSelection()
        {
            if (!_hasMovedSelection)
            {
                // Initial button press
                _selectedButtonIndex = 1;
                _hasMovedSelection = true;
            }
            else if (_selectedButtonIndex > 0)
            {
                _selectedButtonIndex -= 1;
            }
        }

        private void Update()
        {
            // Inputs:
            // - Trigger (any controller): Advance instructions page, (hold) select button
            // - Joystick (any controller): Directional selection of buttons
            if (_isInputEnabled)
            {
                // Get the current input state across both controllers
                var inputs = VRInput.PollAllInput();

                // Handle input on a stimulus screen
                if (IsStimulusScreen() && _isInputReset)
                {
                    // Take action as specified by the inputs
                    ApplyInputs(inputs);

                    if (!VRInput.AnyInput())
                    {
                        ResetButtons();
                    }
                }
                else
                {
                    // Trigger controls
                    if (VRInput.LeftTrigger() || VRInput.RightTrigger())
                    {
                        if (IsInstructionsScreen() && _isInputReset)
                        {
                            if (_uiManager.HasNextPage())
                            {
                                // If pagination has next page, advance
                                _uiManager.NextPage();
                                SetIsInputEnabled(true);

                                // Trigger controller haptics
                                VRInput.SetHaptics(15.0f, 0.4f, 0.1f, true, true);

                                // Update the "Next" button if the last page
                                if (!_uiManager.HasNextPage())
                                {
                                    _uiManager.SetRightButtonState(true, true, "Continue");
                                }
                            }
                            else
                            {
                                // Trigger controller haptics
                                VRInput.SetHaptics(15.0f, 0.4f, 0.1f, true, true);

                                EndTrial();
                            }
                            _isInputReset = false;
                        }
                    }
                }

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
