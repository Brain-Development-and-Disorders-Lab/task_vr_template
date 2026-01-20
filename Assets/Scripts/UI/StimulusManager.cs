using UnityEngine;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Enum to define known stimuli types within the task
    /// </summary>
    public enum EStimulusType
    {
        Default = 0,
    }

    public class StimulusManager : MonoBehaviour
    {
        [Header("Anchors and visual parameters")]
        [SerializeField]
        private GameObject _stimulusAnchor;
        [SerializeField]
        private GameObject _fixationAnchor;

        // Calculated dimensions of stimuli
        [Tooltip("Global scaling factor used to increase or decrease size of all stimuli relatively")]
        [SerializeField]
        private float _scalingFactor = 1.5f;
        private float _stimulusDistance;
        private readonly float _lineWidth = 0.04f; // Specified in supplementary materials
        private float _lineWorldWidth;
        private readonly float _fixationDiameter = 0.5f; // Adapted from supplementary materials
        private float _fixationWorldRadius;
        
        // Stimuli groups, assembled from individual components
        private readonly Dictionary<EStimulusType, List<GameObject>> _stimuliCollection = new();
        private readonly Dictionary<EStimulusType, bool> _stimuliVisibility = new();
        
        // Core GameObjects
        private GameObject _fixationCross; // Fixation cross parent GameObject
        
        // Timer for preserving consistent update rates
        private float _updateTimer = 0.0f;
        private readonly int _refreshRate = 90; // hertz

        // Initialize StimulusManager
        private void Start()
        {
            // Store stimulus distance for calculations
            _stimulusDistance = Mathf.Abs(transform.position.z - _stimulusAnchor.transform.position.z);
        }

        /// <summary>
        /// Function that takes a `EStimulusType` and returns a list of `GameObject`s containing that stimulus
        /// </summary>
        /// <param name="stimulus">A `EStimulusType` value</param>
        /// <returns>Stimulus represented by a list of `GameObject`s</returns>
        public List<GameObject> CreateStimulus(EStimulusType stimulus)
        {
            List<GameObject> StaticComponents = new();
            switch (stimulus)
            {
                case EStimulusType.Default:
                    // Generate default (test) stimulus
                    StaticComponents.Add(CreateRectangle(1.0f, 1.0f, new Vector2(0.0f, 0.0f), Color.white));
                    break;
                default:
                    Debug.LogError("Unknown Stimulus type: " + stimulus);
                    break;
            }
            return StaticComponents;
        }

        /// <summary>
        /// Update the visibility status of a stimulus
        /// </summary>
        /// <param name="stimulus">Exact `EStimulusType`</param>
        /// <param name="visibility">Visibility state, `true` is visible, `false` is not</param>
        public void SetVisible(EStimulusType stimulus, bool visibility)
        {
            // Apply visibility to general stimuli components
            _stimuliCollection.TryGetValue(stimulus, out var StimuliGroup);

            if (StimuliGroup.Count > 0)
            {
                foreach (var component in StimuliGroup)
                {
                    component.SetActive(visibility);
                }
                _stimuliVisibility[stimulus] = visibility;
            }
            else
            {
                Debug.LogWarning("Could not apply visibility to Stimulus: " + stimulus);
            }
        }

        /// <summary>
        /// Utility function to either hide or show all stimuli at once
        /// </summary>
        /// <param name="visibility">Visibility state of all stimuli, `true` shows all, `false` hides all</param>
        public void SetVisibleAll(bool visibility)
        {
            foreach (var key in _stimuliCollection.Keys)
            {
                SetVisible(key, visibility);
            }
        }

        /// <summary>
        /// Utility function to get the scaling factor applied to all visual stimuli
        /// </summary>
        /// <returns>`float` greater than or equal to `1.0f`</returns>
        public float GetScalingFactor() => _scalingFactor;

        /// <summary>
        /// Utility function to create a 2D rectangle with an outline and no fill
        /// </summary>
        /// <param name="width">Width in world units</param>
        /// <param name="height">Height in world units</param>
        /// <param name="position">`Vector2` position of the rectangle center</param>
        /// <param name="color">Color of the rectangle outline</param>
        /// <returns>A `GameObject` containing the rectangle</returns>
        public GameObject CreateRectangle(float width, float height, Vector2 position, Color color)
        {
            // Create base GameObject
            GameObject rectangleObject = new("rdk_rectangle_object");
            rectangleObject.AddComponent<LineRenderer>();
            rectangleObject.transform.SetParent(_stimulusAnchor.transform, false);
            rectangleObject.SetActive(false);

            // Calculate fixed adjustments to center around `position` parameter
            float xOffset = Mathf.Abs(width / 2);
            float yOffset = Mathf.Abs(height / 2);

            var rectangleLine = rectangleObject.GetComponent<LineRenderer>();
            rectangleLine.loop = true;
            rectangleLine.useWorldSpace = false;
            rectangleLine.positionCount = 4;
            Vector3[] linePositions = {
                new(position.x - xOffset, position.y - yOffset, 0.0f),
                new(position.x - xOffset + width, position.y - yOffset, 0.0f),
                new(position.x - xOffset + width, position.y - yOffset + height, 0.0f),
                new(position.x - xOffset, position.y - yOffset + height, 0.0f)
            };
            rectangleLine.SetPositions(linePositions);
            rectangleLine.material = new Material(Resources.Load<Material>("Materials/DefaultWhite"));
            rectangleLine.material.SetColor("_Color", color);
            rectangleLine.startWidth = _lineWorldWidth;
            rectangleLine.endWidth = _lineWorldWidth;

            return rectangleObject;
        }

        /// <summary>
        /// Instantiate a fixation cross `GameObject` and optionally specify a color
        /// </summary>
        /// <param name="color">Color of the cross, namely "white", "red", or "green"</param>
        /// <returns>Fixation cross `GameObject`</returns>
        public GameObject CreateFixationCross(string color = "white")
        {
            var CrossColor = Color.white;
            if (color == "red")
            {
                CrossColor = Color.red;
            }
            else if (color == "green")
            {
                CrossColor = Color.green;
            }

            // Create base GameObject
            GameObject fixationObjectParent = new("rdk_fixation_object");
            fixationObjectParent.transform.SetParent(_fixationAnchor.transform, false);
            fixationObjectParent.SetActive(false);

            // Create horizontal component
            GameObject fixationObjectHorizontal = new("rdk_fixation_object_h");
            fixationObjectHorizontal.AddComponent<LineRenderer>();
            fixationObjectHorizontal.transform.SetParent(fixationObjectParent.transform, false);

            // Create horizontal LineRenderer
            var horizontalLine = fixationObjectHorizontal.GetComponent<LineRenderer>();
            horizontalLine.useWorldSpace = false;
            horizontalLine.positionCount = 2;
            horizontalLine.SetPosition(0, new Vector3(-_fixationWorldRadius, 0.0f, 0.0f));
            horizontalLine.SetPosition(1, new Vector3(_fixationWorldRadius, 0.0f, 0.0f));
            horizontalLine.material = new Material(Resources.Load<Material>("Materials/DefaultWhite"));
            horizontalLine.material.SetColor("_Color", CrossColor);
            horizontalLine.startWidth = _lineWorldWidth / 1.8f;
            horizontalLine.endWidth = _lineWorldWidth / 1.8f;

            // Create vertical component
            GameObject fixationObjectVertical = new("rdk_fixation_object_v");
            fixationObjectVertical.AddComponent<LineRenderer>();
            fixationObjectVertical.transform.SetParent(fixationObjectParent.transform, false);

            // Create vertical LineRenderer
            var verticalLine = fixationObjectVertical.GetComponent<LineRenderer>();
            verticalLine.useWorldSpace = false;
            verticalLine.positionCount = 2;
            verticalLine.SetPosition(0, new Vector3(0.0f, -_fixationWorldRadius, 0.0f));
            verticalLine.SetPosition(1, new Vector3(0.0f, _fixationWorldRadius, 0.0f));
            verticalLine.material = new Material(Resources.Load<Material>("Materials/DefaultWhite"));
            verticalLine.material.SetColor("_Color", CrossColor);
            verticalLine.startWidth = _lineWorldWidth / 1.8f;
            verticalLine.endWidth = _lineWorldWidth / 1.8f;

            return fixationObjectParent;
        }

        public void SetFixationCrossVisibility(bool isVisible) => _fixationCross.SetActive(isVisible);

        public void SetFixationCrossColor(string color)
        {
            var renderers = _fixationCross.GetComponentsInChildren<LineRenderer>();
            foreach (var renderer in renderers)
            {
                if (color == "white")
                {
                    renderer.material.SetColor("_Color", Color.white);
                }
                else if (color == "red")
                {
                    renderer.material.SetColor("_Color", Color.red);
                }
                else if (color == "green")
                {
                    renderer.material.SetColor("_Color", Color.green);
                }
            }
        }

        /// <summary>
        /// Get a reference to the StimulusAnchor `GameObject`
        /// </summary>
        /// <returns>StimulusAnchor `GameObject`</returns>
        public GameObject GetStimulusAnchor() => _stimulusAnchor;

        /// <summary>
        /// Get a reference to the FixationAnchor `GameObject`
        /// </summary>
        /// <returns>FixationAnchor `GameObject`</returns>
        public GameObject GetFixationAnchor() => _fixationAnchor;

        private void Update()
        {
            _updateTimer += Time.deltaTime;

            // Apply updates at the frequency of the desired refresh rate
            if (_updateTimer >= 1.0f / _refreshRate)
            {
                // Reset the update timer
                _updateTimer = 0.0f;
            }
        }
    }
}

