using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace UI
{
    public class UIManager : MonoBehaviour
    {
        [SerializeField]
        private Canvas _uiCanvas;
        [SerializeField]
        private float _scalingFactor = 0.1f; // Adjust scaling of stimulus to be viewable
        [SerializeField]
        private float _verticalOffset = -2.0f; // Adjust the vertical positioning of the UI

        // Text UI components
        private GameObject _headerContainer;
        private string _headerText = "";
        private TextMeshProUGUI _headerTextComponent;
        private GameObject _bodyContainer;
        private string _bodyText = "";
        private TextMeshProUGUI _bodyTextComponent;

        // Button UI components
        private GameObject _leftButton;
        private GameObject _rightButton;

        // Page UI components
        private bool _usePagination;
        private List<string> _pageContent;
        private int _activePage = 0;

        private void Start()
        {
            // Run setup functions to create and position UI components
            SetupUI();
            SetVisible(false);
        }

        private void SetupUI()
        {
            if (_uiCanvas == null)
            {
                Debug.LogError("No _uiCanvas specified. UI will not appear!");
                SetVisible(false);
                return;
            }

            // Update the _uiCanvas positioning to incorporate a vertical offset
            _uiCanvas.transform.position = new Vector3(0.0f, 10.0f * _verticalOffset, 200.0f);

            // Create GameObject for header
            _headerContainer = new("rdk_text_header_container");
            _headerContainer.AddComponent<TextMeshProUGUI>();
            _headerContainer.transform.SetParent(_uiCanvas.transform, false);
            _headerContainer.SetActive(true);
            _headerContainer.transform.localScale = new Vector3(_scalingFactor, _scalingFactor, _scalingFactor);

            // Header component (10%, top)
            _headerTextComponent = _headerContainer.GetComponent<TextMeshProUGUI>();
            _headerTextComponent.text = _headerText;
            _headerTextComponent.fontStyle = FontStyles.Bold;
            _headerTextComponent.fontSize = 8.0f;
            _headerTextComponent.material.color = Color.white;
            _headerTextComponent.alignment = TextAlignmentOptions.Center;
            _headerTextComponent.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            _headerTextComponent.transform.localPosition = new Vector3(0.0f, 40.0f, 0.0f);
            _headerTextComponent.rectTransform.sizeDelta = new Vector2(180.0f, 14.0f);

            // Create GameObject for body
            _bodyContainer = new("rdk_text_body_object");
            _bodyContainer.AddComponent<TextMeshProUGUI>();
            _bodyContainer.transform.SetParent(_uiCanvas.transform, false);
            _bodyContainer.SetActive(true);
            _bodyContainer.transform.localScale = new Vector3(_scalingFactor, _scalingFactor, _scalingFactor);

            // Body component (80%, below header)
            _bodyTextComponent = _bodyContainer.GetComponent<TextMeshProUGUI>();
            _bodyTextComponent.text = _bodyText;
            _bodyTextComponent.fontSize = 6.0f;
            _bodyTextComponent.material.color = Color.white;
            _bodyTextComponent.alignment = TextAlignmentOptions.Center;
            _bodyTextComponent.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            _bodyTextComponent.transform.localPosition = new Vector3(0.0f, -5.0f, 0.0f);
            _bodyTextComponent.rectTransform.sizeDelta = new Vector2(160.0f, 100.0f);

            // Button components (10%, below body)
            GameObject buttonBodyObject = new("rdk_button_body_object");
            buttonBodyObject.transform.SetParent(_uiCanvas.transform, false);
            buttonBodyObject.transform.localPosition = new Vector3(0.0f, -60.0f, 0.0f);

            TMP_DefaultControls.Resources ButtonResources = new();

            // Left button, typically "back" action
            _leftButton = TMP_DefaultControls.CreateButton(ButtonResources);
            _leftButton.transform.SetParent(buttonBodyObject.transform, false);
            _leftButton.transform.localPosition = new Vector3(-42.5f, 0.0f, 0.0f);
            _leftButton.GetComponent<RectTransform>().sizeDelta = new Vector2(28.0f, 14.0f);
            _leftButton.GetComponent<Image>().sprite = Resources.Load<Sprite>("Sprites/Button");
            var LButtonText = _leftButton.GetComponentInChildren<TextMeshProUGUI>();
            LButtonText.fontStyle = FontStyles.Bold;
            LButtonText.fontSize = 5.0f;
            LButtonText.text = "Back";

            // Right button, typically "next" action
            _rightButton = TMP_DefaultControls.CreateButton(ButtonResources);
            _rightButton.transform.SetParent(buttonBodyObject.transform, false);
            _rightButton.transform.localPosition = new Vector3(42.5f, 0.0f, 0.0f);
            _rightButton.GetComponent<RectTransform>().sizeDelta = new Vector2(28.0f, 14.0f);
            _rightButton.GetComponent<Image>().sprite = Resources.Load<Sprite>("Sprites/Button");
            var RButtonText = _rightButton.GetComponentInChildren<TextMeshProUGUI>();
            RButtonText.fontStyle = FontStyles.Bold;
            RButtonText.fontSize = 5.0f;
            RButtonText.text = "Next";
        }

        public void SetHeaderText(string text)
        {
            _headerText = text;
            _headerTextComponent.text = _headerText;
        }

        public void SetBodyText(string text)
        {
            _bodyText = text;
            _bodyTextComponent.text = _bodyText;
        }

        public void EnablePagination(bool state) => _usePagination = state;

        public int GetCurrentActivePage() => _activePage;

        public void SetPages(List<string> pages)
        {
            _pageContent = pages;
            _activePage = 0; // Reset the active page index
            _usePagination = pages.Count > 1;
            SetPage(_activePage);
        }

        public void SetPage(int pageIndex)
        {
            if (pageIndex >= 0 && pageIndex < _pageContent.Count)
            {
                _activePage = pageIndex;
                SetBodyText(_pageContent[_activePage]);

                // Update the button state
                _leftButton.GetComponent<Button>().interactable = HasPreviousPage();
                _rightButton.GetComponent<Button>().interactable = HasNextPage();
            }
            else
            {
                Debug.LogWarning("Invalid page index specified");
            }
        }

        public bool HasNextPage() => _usePagination && _activePage + 1 < _pageContent.Count;

        public void NextPage()
        {
            if (HasNextPage())
            {
                SetPage(_activePage + 1);
            }
        }

        public bool HasPreviousPage() => _usePagination && _activePage - 1 >= 0;

        public void PreviousPage()
        {
            if (HasPreviousPage())
            {
                SetPage(_activePage - 1);
            }
        }

        public void SetLeftButtonState(bool enabled, bool visible = true, string text = "Back")
        {
            _leftButton.GetComponentInChildren<TextMeshProUGUI>().text = text;
            _leftButton.GetComponent<Button>().interactable = enabled;
            _leftButton.SetActive(visible);
        }

        public void SetRightButtonState(bool enabled, bool visible = true, string text = "Next")
        {
            _rightButton.GetComponentInChildren<TextMeshProUGUI>().text = text;
            _rightButton.GetComponent<Button>().interactable = enabled;
            _rightButton.SetActive(visible);
        }

        public void ClickLeftButton() => ExecuteEvents.Execute(_leftButton, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);

        public void ClickRightButton() => ExecuteEvents.Execute(_rightButton, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);

        public void SetVisible(bool state)
        {
            // Text components
            _headerContainer.SetActive(state);
            _bodyContainer.SetActive(state);

            // Button components
            _leftButton.SetActive(state);
            _rightButton.SetActive(state);
        }
    }
}
