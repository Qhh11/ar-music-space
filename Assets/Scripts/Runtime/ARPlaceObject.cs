using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

namespace UnityEngine.XR.ARFoundation.Samples
{
    public class ARPlaceObject : MonoBehaviour
    {
        const float k_PrefabHalfSize = 0.025f;

        [SerializeField]
        [Tooltip("The prefab to be placed or moved.")]
        GameObject m_PrefabToPlace;

        [SerializeField]
        [Tooltip("The Scriptable Object Asset that contains the ARRaycastHit event.")]
        ARRaycastHitEventAsset m_RaycastHitEvent;

        [Header("Instrument Selection")]
        [SerializeField]
        [Tooltip("Array of different instrument prefabs to choose from")]
        GameObject[] m_InstrumentPrefabs;
        [SerializeField]
        [Tooltip("Names for each instrument (should match prefabs array)")]
        string[] m_InstrumentNames;
        
        [Header("UI References")]
        [SerializeField]
        [Tooltip("UI Dropdown for instrument selection")]
        TMP_Dropdown m_InstrumentDropdown;

        [SerializeField]
        [Tooltip("Maximum number of instruments when in multiple mode")]
        int m_MaxInstruments = 5;
        
        //private objects
        GameObject m_SpawnedObject;
        List<GameObject> m_PlacedInstruments = new List<GameObject>();
        int m_CurrentInstrumentIndex = 0;

        void Start()
        {
            SetupUI();
        }
        
        public GameObject prefabToPlace
        {
            get => m_PrefabToPlace;
            set => m_PrefabToPlace = value;
        }
        
        public GameObject spawnedObject
        {
            get => m_SpawnedObject;
            set => m_SpawnedObject = value;
        }
        
        public int currentInstrumentIndex
        {
            get => m_CurrentInstrumentIndex;
            set => SetInstrumentIndex(value);
        }
        
        void OnEnable()
        {
            if (m_RaycastHitEvent == null || m_PrefabToPlace == null)
            {
                Debug.LogWarning($"{nameof(ARPlaceObject)} component on {name} has null inputs and will have no effect in this scene.", this);
                return;
            }

            if (m_RaycastHitEvent != null)
                m_RaycastHitEvent.eventRaised += PlaceObjectAt;
        }

        void OnDisable()
        {
            if (m_RaycastHitEvent != null)
                m_RaycastHitEvent.eventRaised -= PlaceObjectAt;
        }

  
        void SetupUI()
        {
            if (m_InstrumentDropdown != null)
            {
                Debug.Log("[ARPlaceObject] Setting up dropdown...");
                m_InstrumentDropdown.ClearOptions();
                
                List<string> options = new List<string>();
                
                // Add instrument names or generate from prefab names
                if (m_InstrumentNames != null && m_InstrumentNames.Length == m_InstrumentPrefabs?.Length)
                {
                    options.AddRange(m_InstrumentNames);
                    Debug.Log($"[ARPlaceObject] Added {m_InstrumentNames.Length} custom names");
                }
                else if (m_InstrumentPrefabs != null)
                {
                    foreach (var prefab in m_InstrumentPrefabs)
                    {
                        options.Add(prefab != null ? prefab.name : "Unknown");
                        Debug.Log($"[ARPlaceObject] Added {m_InstrumentPrefabs.Length} prefab names");
                    }
                }
                else
                {
                    options.Add("Default Prefab");
                    Debug.Log("[ARPlaceObject] Added default option");
                }

                m_InstrumentDropdown.AddOptions(options);
                m_InstrumentDropdown.onValueChanged.AddListener(OnInstrumentChanged);
                m_InstrumentDropdown.value = m_CurrentInstrumentIndex;
            }
            
        }
        
        GameObject GetCurrentPrefab()
        {
            if (m_InstrumentPrefabs != null && 
                m_CurrentInstrumentIndex >= 0 && 
                m_CurrentInstrumentIndex < m_InstrumentPrefabs.Length)
            {
                return m_InstrumentPrefabs[m_CurrentInstrumentIndex];
            }
            
            return m_PrefabToPlace; 
        }
        
        void OnInstrumentChanged(int newIndex)
        {
            SetInstrumentIndex(newIndex);
        }

        void SetInstrumentIndex(int index)
        {
            if (m_InstrumentPrefabs != null && index >= 0 && index < m_InstrumentPrefabs.Length)
            {
                m_CurrentInstrumentIndex = index;
                Debug.Log($"Selected instrument: {GetCurrentPrefab()?.name}");
            }
        }
        

        void PlaceObjectAt(object sender, ARRaycastHit hitPose)
        {
            GameObject prefabToSpawn = GetCurrentPrefab();
            if (prefabToSpawn == null)
            {
                Debug.LogWarning("No prefab selected to place!");
                return;
            }

            PlaceMultipleInstrument(hitPose, prefabToSpawn);
        }
        
       
        void PlaceMultipleInstrument(ARRaycastHit hitPose, GameObject prefabToSpawn)
        {
            if (m_PlacedInstruments.Count >= m_MaxInstruments)
            {
                Debug.Log($"Maximum instruments reached: {m_MaxInstruments}");
                return;
            }

            GameObject newInstrument = Instantiate(prefabToSpawn, hitPose.trackable.transform.parent);
            
            var forward = hitPose.pose.rotation * Vector3.up;
            var offset = forward * (k_PrefabHalfSize + 0.02f);
            newInstrument.transform.position = hitPose.pose.position + offset;
            newInstrument.transform.parent = hitPose.trackable.transform.parent;

            SetupInstrumentRendering(newInstrument);
            
            m_PlacedInstruments.Add(newInstrument);
            m_SpawnedObject = newInstrument; // Keep reference to latest

            Debug.Log($"Placed instrument #{m_PlacedInstruments.Count}: {prefabToSpawn.name}");
        }

        void SetupInstrumentRendering(GameObject instrument)
        {
            Renderer[] renderers = instrument.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null && renderer.material != null)
                {
                    renderer.material.renderQueue = 3000;
                }
            }
        }

        public void DeleteAllInstruments()
        {
            foreach (GameObject instrument in m_PlacedInstruments)
            {
                if (instrument != null)
                    Destroy(instrument);
            }
            m_PlacedInstruments.Clear();
            m_SpawnedObject = null;
            Debug.Log("Deleted all instruments");
        }


    }
    
}


