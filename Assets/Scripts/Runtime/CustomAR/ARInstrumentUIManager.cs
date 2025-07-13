using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEngine.XR.ARFoundation.Samples
{
    public class ARInstrumentUIManager : MonoBehaviour
    {
        [Header("AR Components")] [SerializeField]
        ARPlaceObject m_ARPlaceObject;

        [SerializeField] ARSession m_ARSession;

        [Header("UI References")] [SerializeField]
        TMP_Dropdown m_InstrumentDropdown;

        [SerializeField] Button m_ResetButton;
        [SerializeField] Button m_DeleteButton;
        [SerializeField] Slider m_ScaleSlider;

        void Start()
        {
            InitializeComponents();
            SetupButtonListeners();
        }


        void InitializeComponents()
        {
            if (m_ARPlaceObject == null)
                m_ARPlaceObject = FindObjectOfType<ARPlaceObject>();

            if (m_ARSession == null)
                m_ARSession = FindObjectOfType<ARSession>();
        }


        void SetupButtonListeners()
        {
            if (m_ResetButton != null)
            {
                m_ResetButton.onClick.AddListener(OnResetClicked);
            }

            if (m_InstrumentDropdown != null && m_ARPlaceObject != null)
            {
                m_InstrumentDropdown.onValueChanged.AddListener(OnInstrumentSelectionChanged);
            }

            if (m_ScaleSlider != null) m_ScaleSlider.onValueChanged.AddListener(OnScaleSliderChanged);
            if (m_DeleteButton != null) m_DeleteButton.onClick.AddListener(OnDeleteClicked);

        }


        void OnResetClicked()
        {
            Debug.Log("[UI] Reset button clicked");

            if (m_ARPlaceObject != null)
            {
                m_ARPlaceObject.DeleteAllInstruments();
            }

            if (m_ARSession != null)
            {
                m_ARSession.Reset();
            }
        }


        void OnInstrumentSelectionChanged(int newIndex)
        {
            Debug.Log($"[UI] Instrument selection changed to index: {newIndex}");
            if (m_ARPlaceObject != null)
            {
                m_ARPlaceObject.currentInstrumentIndex = newIndex;
            }
        }
        
        void OnScaleSliderChanged(float value)
        {     Debug.Log($"[UI] Scale slider changed: {value}");
            if (m_ARPlaceObject != null)
            {
                m_ARPlaceObject.ScaleCurrentInstrument(value);
            }

        }

        void OnDeleteClicked()
        {
            Debug.Log("[UI] Delete All button clicked");
            if (m_ARPlaceObject != null)
            {
                m_ARPlaceObject.DeleteAllInstruments();
            }
        }
    }
}
