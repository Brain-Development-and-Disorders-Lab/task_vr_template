using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Utilities
{
    public class VRLogger : MonoBehaviour
    {
        [SerializeField]
        private GameObject _logCanvas;

        [SerializeField]
        private Font _font;

        [SerializeField]
        private bool _showLogger = true;

        // Data collections
        private List<string> _messages;
        private List<GameObject> _messageContainers;

        [SerializeField]
        private int _messageLimit = 20; // Set the number of message rows displayed
        [SerializeField]
        private bool _useHeadTracking = true; // Fix the logger to headset head tracking position

        private void CreateLoggerRows()
        {
            // Iterate over the list of _messages
            for (int i = 0; i < _messageLimit; i++)
            {
                GameObject textContainer = new("logger_message_" + i.ToString());
                textContainer.transform.position = new Vector3(150.0f, 0.0f - (15.0f * i), 0.0f);
                textContainer.layer = 5; // UI layer
                textContainer.transform.SetParent(_logCanvas.transform, false);

                var text = textContainer.AddComponent<Text>();
                text.font = _font;
                text.color = Color.green;
                text.alignment = TextAnchor.MiddleLeft;
                text.fontSize = 8;
                var textTransform = textContainer.GetComponent<RectTransform>();
                textTransform.sizeDelta = new Vector2(300.0f, 15.0f);
                textTransform.anchorMin = new Vector2(0, 1);
                textTransform.anchorMax = new Vector2(0, 1);

                _messageContainers.Add(textContainer);
            }
        }

        private void RenderMessages()
        {
            for (int i = 0; i < _messages.Count; i++)
            {
                var textContainer = _messageContainers[i];
                var messageText = textContainer.GetComponent<Text>();
                messageText.text = _messages[i];

                // Toggle visibility
                textContainer.SetActive(_showLogger);
            }
        }

        private void Start()
        {
            // Create new arrays
            _messages = new List<string>();
            _messageContainers = new List<GameObject>();

            // Instatiate the rows
            CreateLoggerRows();

            // Fix to head movement if in VR context
            if (_useHeadTracking)
            {
                var cameraRig = FindFirstObjectByType<OVRCameraRig>();
                if (cameraRig)
                {
                    _logCanvas.transform.SetParent(cameraRig.centerEyeAnchor.transform, false);
                }
            }
        }

        private void Update() => RenderMessages();

        public void Log(string message)
        {
            Debug.Log("VRLogger: " + message);
            message = DateTime.Now.ToString("T") + ": " + message;
            _messages.Add(message);
            if (_messages.Count > _messageLimit)
            {
                _messages.RemoveAt(0);
                _messages.TrimExcess();
            }
        }

        /// <summary>
        /// Set the visibility of the `VRLogger` class
        /// </summary>
        /// <param name="visible">State of the logger visibility</param>
        public void SetVisible(bool visible) => _showLogger = visible;
    }
}
