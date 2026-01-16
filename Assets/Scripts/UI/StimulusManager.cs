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
        Fixation = 0,
        Decision = 1,
        Motion = 2,
        Feedback_Correct = 3,
        Feedback_Incorrect = 4
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
        private readonly float _apertureWidth = 8.0f; // Degrees
        private float _apertureWorldWidth; // Calculated from degrees into world units
        private float _apertureWorldHeight; // Calculated from degrees into world units
        private readonly float _lineWidth = 0.04f; // Specified in supplementary materials
        private float _lineWorldWidth;
        private readonly float _fixationDiameter = 0.5f; // Adapted from supplementary materials
        private float _fixationWorldRadius;

        // Dot parameters
        private readonly float _dotDiameter = 0.12f; // Specified in supplementary materials
        private float _dotWorldRadius;
        private readonly List<Dot> _dots = new();
        private float _dotCoherence = 0.2f; // Default training coherence
        private float _dotDirection = (float)Math.PI; // "reference" dot type direction
        private readonly float _dotDensity = 16.0f;
        private int _dotCount = 0;

        // Timer for preserving consistent update rates
        private float _updateTimer = 0.0f;
        private readonly int _refreshRate = 90; // hertz

        // Stimuli groups, assembled from individual components
        private readonly Dictionary<EStimulusType, List<GameObject>> _stimuliCollection = new();
        private readonly Dictionary<EStimulusType, bool> _stimuliVisibility = new();
        private GameObject _fixationCross; // Fixation cross parent GameObject

        // UI cursor for selecting buttons
        public enum ECursorSide
        {
            Left,
            Right,
        }
        private GameObject _cursor; // Cursor parent GameObject
        private ECursorSide _activeCursorSide = ECursorSide.Left;

        // Slider-based button prefab
        [Header("Prefabs")]
        [SerializeField]
        private GameObject _buttonPrefab;
        private ButtonSliderInput[] _buttonSliders = new ButtonSliderInput[4];

        // Initialize StimulusManager
        private void Start()
        {
            CalculateWorldSizing(); // Run pre-component calculations to ensure consistent world sizing

            // Additional UI elements to be controlled outside the stimuli
            _fixationCross = CreateFixationCross();
            _cursor = CreateCursor();

            foreach (EStimulusType stimuli in Enum.GetValues(typeof(EStimulusType)))
            {
                // Create the named set of components and store
                _stimuliCollection.Add(stimuli, CreateStimulus(stimuli));
                _stimuliVisibility.Add(stimuli, false);
            }
        }

        /// <summary>
        /// Setup function resposible for calculations to convert all degree-based sizes into world units
        /// </summary>
        private void CalculateWorldSizing()
        {
            // Store stimulus distance for calculations
            _stimulusDistance = Mathf.Abs(transform.position.z - _stimulusAnchor.transform.position.z);

            // Aperture dimensions
            _apertureWorldWidth = _scalingFactor * _stimulusDistance * Mathf.Tan(_apertureWidth * (Mathf.PI / 180.0f));
            _apertureWorldHeight = _apertureWorldWidth * 2.0f;

            // Line-related dimensions
            _lineWorldWidth = _scalingFactor * _lineWidth;
            _fixationWorldRadius = _scalingFactor * _stimulusDistance * Mathf.Tan(_fixationDiameter / 2.0f * (Mathf.PI / 180.0f));

            // Dot dimensions and count, scaled for desired density
            _dotWorldRadius = _scalingFactor * _stimulusDistance * Mathf.Tan(_dotDiameter / 2.0f * (Mathf.PI / 180.0f));
            _dotCount = Mathf.RoundToInt(_scalingFactor * _dotDensity * _apertureWidth * _apertureWidth * 2.0f * _stimulusDistance * Mathf.Tan(Mathf.PI / 180.0f));
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
                case EStimulusType.Fixation:
                    // Generate aperture
                    StaticComponents.Add(CreateRectangle(_apertureWorldWidth, _apertureWorldHeight, new Vector2(0.0f, 0.0f), Color.white));
                    break;
                case EStimulusType.Decision:
                    // Generate aperture
                    StaticComponents.Add(CreateRectangle(_apertureWorldWidth, _apertureWorldHeight, new Vector2(0.0f, 0.0f), Color.white));
                    // Add selection buttons
                    StaticComponents.Add(CreateDecisionButtons());
                    break;
                case EStimulusType.Motion:
                    // Generate aperture
                    StaticComponents.Add(CreateRectangle(_apertureWorldWidth, _apertureWorldHeight, new Vector2(0.0f, 0.0f), Color.white));
                    // Add _dots
                    CreateDots();
                    break;
                case EStimulusType.Feedback_Correct:
                    // Generate aperture
                    StaticComponents.Add(CreateRectangle(_apertureWorldWidth, _apertureWorldHeight, new Vector2(0.0f, 0.0f), Color.white));
                    break;
                case EStimulusType.Feedback_Incorrect:
                    // Generate aperture
                    StaticComponents.Add(CreateRectangle(_apertureWorldWidth, _apertureWorldHeight, new Vector2(0.0f, 0.0f), Color.white));
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
            // Apply visibility to _dots separately, if this is a stimulus that uses _dots
            if (stimulus == EStimulusType.Motion)
            {
                foreach (var dot in _dots)
                {
                    // Only set the dot to be visible if it is within the aperture
                    if (visibility && Mathf.Sqrt(Mathf.Pow(dot.GetPosition().x, 2.0f) + Mathf.Pow(dot.GetPosition().y, 2.0f)) <= _apertureWorldWidth / 2.0f)
                    {
                        dot.SetVisible(true);
                    }
                    else
                    {
                        dot.SetVisible(false);
                    }
                }
            }

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

        public void CreateDots()
        {
            for (int i = 0; i < _dotCount; i++)
            {
                float x = UnityEngine.Random.Range(-_apertureWorldWidth / 2.0f, _apertureWorldWidth / 2.0f);
                float y = UnityEngine.Random.Range(-_apertureWorldHeight / 2.0f, _apertureWorldHeight / 2.0f);

                string dotBehavior = UnityEngine.Random.value > _dotCoherence ? "random" : "reference";
                _dots.Add(new Dot(_stimulusAnchor, _dotWorldRadius, _apertureWorldWidth, _apertureWorldHeight, dotBehavior, x, y, false));
            }
        }

        public GameObject CreateCursor()
        {
            GameObject cursorObject = new("rdk_cursor_object");
            cursorObject.transform.SetParent(_stimulusAnchor.transform, false);
            cursorObject.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
            cursorObject.transform.localScale = new Vector2(0.2f, 0.2f);
            cursorObject.SetActive(false);

            cursorObject.AddComponent<SpriteRenderer>();
            cursorObject.GetComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>("Sprites/Cursor");

            // Flip sprite if displayed to the left of the aperture
            if (_activeCursorSide == ECursorSide.Left)
            {
                cursorObject.GetComponent<SpriteRenderer>().flipX = true;
            }

            return cursorObject;
        }

        public void ResetCursor()
        {
            // Reset the vertical position of the _cursor
            var updatedPosition = _cursor.transform.localPosition;
            updatedPosition.y = 0.0f;
            _cursor.transform.localPosition = updatedPosition;

            SetCursorSide(ECursorSide.Right);
            _cursor.GetComponent<SpriteRenderer>().material.SetColor("_Color", Color.gray);
        }

        public void SetCursorVisiblity(bool state) => _cursor.SetActive(state);

        public void SetCursorSide(ECursorSide side)
        {
            // Update the side and flip if required
            _activeCursorSide = side;
            var updatedPosition = _cursor.transform.localPosition;
            if (_activeCursorSide == ECursorSide.Left)
            {
                _cursor.GetComponent<SpriteRenderer>().flipX = true;
                updatedPosition.x = -1.0f * _apertureWorldWidth * 0.8f;
            }
            else
            {
                _cursor.GetComponent<SpriteRenderer>().flipX = false;
                updatedPosition.x = _apertureWorldWidth * 0.8f;
            }
            _cursor.transform.localPosition = updatedPosition;
        }

        public void SetCursorIndex(int index)
        {
            // Update _cursor color to show it is active
            _cursor.GetComponent<SpriteRenderer>().material.SetColor("_Color", Color.white);

            // Local Y positions of each of the button sliders
            float[] sliderY = { 2.95f, 2.25f, -2.25f, -2.95f };

            // Check for an invalid index
            if (index >= 0 && index < sliderY.Length)
            {
                // Update the vertical position of the _cursor
                var updatedPosition = _cursor.transform.localPosition;
                updatedPosition.y = sliderY[index];

                // Update the _cursor position
                _cursor.transform.localPosition = updatedPosition;
            }
        }

        public GameObject CreateDecisionButtons()
        {
            GameObject buttonDecisionObject = new("rdk_button_decision_object");
            buttonDecisionObject.transform.SetParent(_stimulusAnchor.transform, false);
            buttonDecisionObject.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
            buttonDecisionObject.SetActive(false);

            var V_U_Button = Instantiate(_buttonPrefab, buttonDecisionObject.transform);
            V_U_Button.transform.localPosition = new Vector3(0.0f, 2.95f, 0.0f);
            var V_U_Slider = V_U_Button.GetComponentInChildren<ButtonSliderInput>();
            V_U_Slider.Setup();
            V_U_Slider.SetButtonText("<b>Up</b>\nVery Confident");
            V_U_Slider.SetBackgroundColor(new Color(255f / 255f, 194f / 255f, 10f / 255f)); // Colorblind-friendly
            V_U_Slider.SetFillColor(new Color(255f / 255f, 206f / 255f, 59f / 255f)); // 20% lighter

            var S_U_Button = Instantiate(_buttonPrefab, buttonDecisionObject.transform);
            S_U_Button.transform.localPosition = new Vector3(0.0f, 2.25f, 0.0f);
            var S_U_Slider = S_U_Button.GetComponentInChildren<ButtonSliderInput>();
            S_U_Slider.Setup();
            S_U_Slider.SetButtonText("<b>Up</b>\nSomewhat Confident");
            S_U_Slider.SetBackgroundColor(new Color(255f / 255f, 224f / 255f, 132f / 255f)); // 50% lighter
            S_U_Slider.SetFillColor(new Color(255f / 255f, 230f / 255f, 157f / 255f)); // 20% lighter

            var V_D_Button = Instantiate(_buttonPrefab, buttonDecisionObject.transform);
            V_D_Button.transform.localPosition = new Vector3(0.0f, -2.95f, 0.0f);
            var V_D_Slider = V_D_Button.GetComponentInChildren<ButtonSliderInput>();
            V_D_Slider.Setup();
            V_D_Slider.SetButtonText("<b>Down</b>\nVery Confident");
            V_D_Slider.SetBackgroundColor(new Color(12f / 255f, 123f / 255f, 220f / 255f)); // Colorblind-friendly
            V_D_Slider.SetFillColor(new Color(44f / 255f, 151f / 255f, 243f / 255f)); // 20% lighter

            var S_D_Button = Instantiate(_buttonPrefab, buttonDecisionObject.transform);
            S_D_Button.transform.localPosition = new Vector3(0.0f, -2.25f, 0.0f);
            var S_D_Slider = S_D_Button.GetComponentInChildren<ButtonSliderInput>();
            S_D_Slider.Setup();
            S_D_Slider.SetButtonText("<b>Down</b>\nSomewhat Confident");
            S_D_Slider.SetBackgroundColor(new Color(123f / 255f, 190f / 255f, 248f / 255f)); // 50% lighter
            S_D_Slider.SetFillColor(new Color(149f / 255f, 203f / 255f, 249f / 255f)); // 20% lighter

            // Store the slider controllers in display order (top to bottom)
            _buttonSliders = new ButtonSliderInput[] {
                V_U_Slider,
                S_U_Slider,
                S_D_Slider,
                V_D_Slider,
            };

            return buttonDecisionObject;
        }

        public ButtonSliderInput[] GetButtonSliders() => _buttonSliders;

        public float GetCoherence() => _dotCoherence;

        public void SetCoherence(float coherence)
        {
            // Update the stored coherence value
            if (coherence is >= 0.0f and <= 1.0f)
            {
                _dotCoherence = coherence;
            }

            // Apply the coherence across all _dots
            foreach (var dot in _dots)
            {
                dot.SetBehavior(UnityEngine.Random.value > _dotCoherence ? "random" : "reference");
            }
        }

        public float GetDirection() => _dotDirection;

        public void SetDirection(float direction)
        {
            _dotDirection = direction;

            // Apply the direction across all "reference" and "random" type _dots
            foreach (var dot in _dots)
            {
                if (dot.GetBehavior() == "reference")
                {
                    dot.SetDirection(_dotDirection);
                }
                else
                {
                    dot.SetDirection(UnityEngine.Random.value * 2.0f * Mathf.PI);
                }
            }
        }

        /// <summary>
        /// Get the width in world units of the stimuli. Used for correct offset calculations for dioptic / dichoptic
        /// stimulus presentation.
        /// </summary>
        /// <returns>Stimulus width, measured in world units</returns>
        public float GetApertureWidth() => _apertureWorldWidth;

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
                if (_stimuliVisibility[EStimulusType.Motion])
                {
                    foreach (var dot in _dots)
                    {
                        dot.Update();
                    }
                }

                // Reset the update timer
                _updateTimer = 0.0f;
            }
        }
    }
    
    public class Dot
    {
        private readonly GameObject _dotAnchor;
        private readonly float _dotRadius;
        private readonly float _apertureWidth;
        private readonly float _apertureHeight;
        private string _dotBehavior;
        private float _dotX;
        private float _dotY;
        private float _dotDirection;
        private GameObject _dotObject;
        private SpriteRenderer _dotRenderer;

        // Visibility and activity state
        private bool _dotVisible;
        private bool _dotActive = true;

        public Dot(GameObject anchor, float radius, float apertureWidth, float apertureHeight, string behavior, float x = 0.0f, float y = 0.0f, bool visible = true)
        {
            _dotAnchor = anchor;
            _dotRadius = radius;
            _apertureWidth = apertureWidth;
            _apertureHeight = apertureHeight;
            _dotBehavior = behavior;
            _dotX = x;
            _dotY = y;
            _dotDirection = Mathf.PI / 2; // Default direction is up
            _dotVisible = visible;

            // Update direction depending on initial behaviour
            if (behavior == "random")
            {
                _dotDirection = (float)(2.0f * Math.PI * UnityEngine.Random.value);
            }

            CreateGameObject();
        }

        private GameObject CreateGameObject()
        {
            // Create base GameObject
            _dotObject = new GameObject("rdk_dot_object");
            _dotObject.transform.SetParent(_dotAnchor.transform, false);
            _dotObject.AddComponent<SpriteRenderer>();
            _dotObject.transform.localPosition = new Vector3(_dotX, _dotY, 0.0f);

            // Create SpriteRenderer
            _dotRenderer = _dotObject.GetComponent<SpriteRenderer>();
            _dotRenderer.drawMode = SpriteDrawMode.Sliced;
            _dotRenderer.sprite = Resources.Load<Sprite>("Sprites/Circle");
            _dotRenderer.size = new Vector2(_dotRadius * 2.0f, _dotRadius * 2.0f);
            _dotRenderer.enabled = _dotVisible;

            return _dotObject;
        }

        public GameObject GetGameObject() => _dotObject;

        public void SetActive(bool state)
        {
            _dotActive = state;
            _dotObject.SetActive(_dotActive);
        }

        public void SetVisible(bool state)
        {
            _dotVisible = state;
            _dotRenderer.enabled = _dotVisible;
        }

        public bool GetVisible() => _dotVisible;

        public void SetDirection(float direction) => _dotDirection = direction;

        public Vector2 GetPosition() => new(_dotX, _dotY);

        public void SetPosition(Vector2 position)
        {
            _dotX = position.x;
            _dotY = position.y;
        }

        public string GetBehavior() => _dotBehavior;

        public void SetBehavior(string behavior)
        {
            if (behavior is "random" or "reference")
            {
                _dotBehavior = behavior;
            }
            else
            {
                Debug.LogWarning("Invalid dot behavior: " + behavior);
            }
        }

        /// <summary>
        /// Utility function to check the visibility of a dot with specific coordinates
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <returns></returns>
        private bool IsVisible(float x, float y)
        {
            float halfWidth = _apertureWidth / 2.0f;
            float halfHeight = _apertureHeight / 2.0f;

            float leftBound = -halfWidth;
            float rightBound = halfWidth;
            float topBound = halfHeight;
            float bottomBound = -halfHeight;

            return x >= leftBound && x <= rightBound && y >= bottomBound && y <= topBound;
        }

        public void Update()
        {
            if (_dotActive)
            {
                // Create and store positions
                var originalPosition = _dotObject.transform.localPosition;
                float updatedX = originalPosition.x;
                float updatedY = originalPosition.y;

                // Get and apply visibility state
                bool visibility = IsVisible(updatedX, updatedY);
                SetVisible(visibility);

                // Random direction adjustment every 12 frames
                if (_dotBehavior == "random" && Time.frameCount % 12 == 0)
                {
                    // Adjust the direction
                    float delta = UnityEngine.Random.value;
                    if (UnityEngine.Random.value > 0.5f)
                    {
                        _dotDirection -= Mathf.PI / 4 * delta;
                    }
                    else
                    {
                        _dotDirection += Mathf.PI / 4 * delta;
                    }
                }

                if (_dotBehavior == "reference")
                {
                    // Reset depending on which edge the dot reached
                    if (updatedY > _apertureHeight / 2)
                    {
                        updatedY -= _apertureHeight;
                    }
                    else if (updatedY < -_apertureHeight / 2)
                    {
                        updatedY += _apertureHeight;
                    }
                }
                else if (_dotBehavior == "random")
                {
                    // Reset depending on which edge the dot reached, adding a padding distance to ensure continued
                    // dot visibility
                    if (updatedY > _apertureHeight / 2)
                    {
                        updatedY -= _apertureHeight;
                        updatedY += _dotRadius * 2.0f;
                    }
                    else if (updatedY < -_apertureHeight / 2)
                    {
                        updatedY += _apertureHeight;
                        updatedY -= _dotRadius * 2.0f;
                    }
                    else if (updatedX > _apertureWidth / 2)
                    {
                        updatedX -= _apertureWidth;
                        updatedX += _dotRadius * 2.0f;
                    }
                    else if (updatedX < -_apertureWidth / 2)
                    {
                        updatedX += _apertureWidth;
                        updatedX -= _dotRadius * 2.0f;
                    }
                }

                // Update overall position
                updatedX += 0.01f * Mathf.Cos(_dotDirection);
                updatedY += 0.01f * Mathf.Sin(_dotDirection);

                _dotX = updatedX;
                _dotY = updatedY;

                // Apply transform
                _dotObject.transform.localPosition = new Vector3(updatedX, updatedY, originalPosition.z);
            }
        }
    }
    
    /// <summary>
    /// Utility class attached to the _buttonSlider prefab to manage slider behaviour
    /// </summary>
    public class ButtonSliderInput : MonoBehaviour
    {
        [SerializeField]
        private GameObject _buttonBackground;
        [SerializeField]
        private GameObject _buttonFill;

        private GameObject _buttonSlider;
        private TextMeshProUGUI _buttonSliderText;
        private Slider _buttonSliderComponent;
        private bool _hasSetup = false;

        public void Setup()
        {
            // Get references to all required components
            _buttonSlider = gameObject;
            _buttonSliderComponent = _buttonSlider.GetComponent<Slider>();
            _buttonSliderText = _buttonSlider.GetComponentInChildren<TextMeshProUGUI>();
            _hasSetup = true;
        }

        /// <summary>
        /// Update the text displayed on the button
        /// </summary>
        /// <param name="buttonText">Button text</param>
        public void SetButtonText(string buttonText)
        {
            if (_hasSetup)
            {
                _buttonSliderText.text = buttonText;
            }
        }

        public float GetSliderValue()
        {
            if (_hasSetup)
            {
                return _buttonSliderComponent.value;
            }
            return 0.0f;
        }

        /// <summary>
        /// Update the value of the slider, displayed as a fill
        /// </summary>
        /// <param name="value">[0.0, 1.0]</param>
        public void SetSliderValue(float value)
        {
            if (value < 0.0f)
            {
                // Value must be positive
                _buttonSliderComponent.value = 0.0f;
            }
            else if (value > 1.0f)
            {
                // Value must be less than or equal to `1.0f`
                _buttonSliderComponent.value = 1.0f;
            }
            else
            {
                _buttonSliderComponent.value = value;
            }
        }

        public void SetBackgroundColor(Color color) => _buttonBackground.GetComponentInChildren<Image>().color = color;

        public void SetFillColor(Color color) => _buttonFill.GetComponentInChildren<Image>().color = color;
    }

}

