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
        
        [Header("Placement Settings")]
        [SerializeField]
        [Tooltip("Show preview of placement position")]
        bool m_ShowPlacementPreview = true;
        
        [SerializeField]
        [Tooltip("Material for placement preview (optional)")]
        Material m_PreviewMaterial;
        
        //private objects
        GameObject m_SpawnedObject;
        List<GameObject> m_PlacedInstruments = new List<GameObject>();
        int m_CurrentInstrumentIndex = 0;
        GameObject m_PreviewObject; 

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
            HidePreview();
        }
        
        public void ShowPlacementPreview(Vector3 position, Quaternion rotation)
        {
            if (!m_ShowPlacementPreview) return;
            
            GameObject prefabToPreview = GetCurrentPrefab();
            if (prefabToPreview == null) return;
            
            
            if (m_PreviewObject == null)
            {
                m_PreviewObject = Instantiate(prefabToPreview);
                SetupPreviewObject(m_PreviewObject);
            }
            
            
            m_PreviewObject.transform.position = position;
            m_PreviewObject.transform.rotation = rotation;
            m_PreviewObject.SetActive(true);
        }
        
  
        public void HidePreview()
        {
            if (m_PreviewObject != null)
            {
                m_PreviewObject.SetActive(false);
            }
        }
        
     
        void SetupPreviewObject(GameObject preview)
        {
            // Make preview semi-transparent
            Renderer[] renderers = preview.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    Material[] materials = renderer.materials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (m_PreviewMaterial != null)
                        {
                            materials[i] = m_PreviewMaterial;
                        }
                        else
                        {
                            // Create transparent version of original material
                            materials[i] = new Material(materials[i]);
                            Color color = materials[i].color;
                            color.a = 0.5f; 
                            materials[i].color = color;
                            
                          
                            materials[i].SetFloat("_Mode", 3); 
                            materials[i].SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                            materials[i].SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                            materials[i].SetInt("_ZWrite", 0);
                            materials[i].DisableKeyword("_ALPHATEST_ON");
                            materials[i].EnableKeyword("_ALPHABLEND_ON");
                            materials[i].DisableKeyword("_ALPHAPREMULTIPLY_ON");
                            materials[i].renderQueue = 3000;
                        }
                    }
                    renderer.materials = materials;
                }
            }
            
           
            Collider[] colliders = preview.GetComponentsInChildren<Collider>();
            foreach (Collider collider in colliders)
            {
                Destroy(collider);
            }
        }
        void PlaceMultipleInstrument(ARRaycastHit hitPose, GameObject prefabToSpawn)
        {
            if (m_PlacedInstruments.Count >= m_MaxInstruments)
            {
                Debug.Log($"Maximum instruments reached: {m_MaxInstruments}");
                return;
            }

            var forward = hitPose.pose.rotation * Vector3.up;
            //var offset = forward * (k_PrefabHalfSize + 0.02f);
            var offset = forward * 0.01f;
            Vector3 targetPosition = hitPose.pose.position + offset;

            
            Vector3 safePosition = FindSafePosition(targetPosition, prefabToSpawn, hitPose.trackable.transform);

          
            GameObject newInstrument = Instantiate(prefabToSpawn, hitPose.trackable.transform.parent);
            if (Camera.main != null)
            {
                Vector3 origEuler = newInstrument.transform.eulerAngles;         
                float camY = Camera.main.transform.eulerAngles.y;              

                Vector3 newEuler = new Vector3(origEuler.x, 180+camY+origEuler.y, origEuler.z);

                newInstrument.transform.rotation = Quaternion.Euler(newEuler);
            }
            newInstrument.transform.position = safePosition;
            newInstrument.transform.parent = hitPose.trackable.transform.parent;

           
            SetupInstrumentRendering(newInstrument);
            SetupInstrumentPhysics(newInstrument);

            m_PlacedInstruments.Add(newInstrument);
            m_SpawnedObject = newInstrument;

            Debug.Log($"Placed instrument #{m_PlacedInstruments.Count}: {prefabToSpawn.name}");
        }

         Vector3 FindSafePosition(Vector3 targetPosition, GameObject prefab, Transform planeTransform)
        {
          
            float objectRadius = GetObjectRadius(prefab);
            float searchRadius = objectRadius * 1.05f; 
            
            Debug.Log($"Searching for safe position around {targetPosition} with search radius {searchRadius}");
            
            if (!IsPositionOccupied(targetPosition, searchRadius))
            {
                Debug.Log("Target position is safe");
                return targetPosition;
            }
            

            int maxAttempts = 24; 
            float currentRadius = searchRadius * 0.8f;
            
            for (int ring = 1; ring <= 3; ring++)
            {
                currentRadius = searchRadius * (0.7f + ring * 0.3f);

                for (int i = 0; i < maxAttempts; i++)
                {
                    float angle = (360f / maxAttempts) * i * Mathf.Deg2Rad;
                    Vector3 localOffset = new Vector3(
                        Mathf.Cos(angle) * currentRadius,
                        0,
                        Mathf.Sin(angle) * currentRadius
                    );
                    
                    Vector3 worldOffset = planeTransform.TransformDirection(localOffset);
                    Vector3 candidatePosition = targetPosition + worldOffset;
                    
                    if (!IsPositionOccupied(candidatePosition, searchRadius) && 
                        IsPositionOnPlane(candidatePosition, planeTransform))
                    {
                        Debug.Log($"Found safe position at distance {currentRadius:F2}m from original target");
                        return candidatePosition;
                    }
                }
            }
            
            Debug.LogWarning("Could not find safe position, placing at original location");
            return targetPosition;
        }


        bool IsPositionOccupied(Vector3 position, float radius)
        {
            
            foreach (GameObject instrument in m_PlacedInstruments)
            {
                if (instrument != null)
                {
                    float distance = Vector3.Distance(position, instrument.transform.position);
                    if (distance < radius)
                    {
                        return true;
                    }
                }
            }
            
            // back up
            Collider[] colliders = Physics.OverlapSphere(position, radius);

            foreach (Collider collider in colliders)
            {
                if (m_PlacedInstruments.Contains(collider.gameObject) || 
                    IsChildOfPlacedInstrument(collider.gameObject))
                {
                    return true;
                }
            }
            
            Debug.Log("Position is free");
            return false;
        }
        
        bool IsChildOfPlacedInstrument(GameObject obj)
        {
            Transform current = obj.transform;
            
            while (current != null)
            {
                if (m_PlacedInstruments.Contains(current.gameObject))
                {
                    return true;
                }
                current = current.parent;
            }
            
            return false;
        }
        
        bool IsPositionOnPlane(Vector3 position, Transform planeTransform)
        {
            Vector3 localPosition = planeTransform.InverseTransformPoint(position);
            
            float maxDistance = 5f; // Maximum 5 meters from plane center
            
            return Mathf.Abs(localPosition.x) < maxDistance && 
                   Mathf.Abs(localPosition.z) < maxDistance;
        }

  
        float GetObjectRadius(GameObject obj)
        {
            Collider objCollider = obj.GetComponent<Collider>();
            if (objCollider != null)
            {
                Bounds bounds = objCollider.bounds;
                float radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f;
                if (radius > 0.1f)
                    return radius;
            }
            
            Renderer objRenderer = obj.GetComponent<Renderer>();
            if (objRenderer == null)
                objRenderer = obj.GetComponentInChildren<Renderer>();
                
            if (objRenderer != null)
            {
                Bounds bounds = objRenderer.bounds;
                float radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f;
                if (radius > 0.1f)
                    return radius;
            }
            
            Vector3 scale = obj.transform.localScale;
            float scaleRadius = Mathf.Max(scale.x, scale.z) * 0.5f;

            if (scaleRadius > 0.1f)
                return scaleRadius;
            
            Debug.LogWarning($"Using default radius for {obj.name}");
            return 0.5f; // 50cm default radius
        }
        
        void SetupInstrumentPhysics(GameObject instrument)
        {
            Collider existingCollider = instrument.GetComponent<Collider>();
            
            if (existingCollider == null)
            {
               
                Collider childCollider = instrument.GetComponentInChildren<Collider>();
                
                if (childCollider == null)
                {
                    
                    SphereCollider sphereCollider = instrument.AddComponent<SphereCollider>();
                    float radius = GetObjectRadius(instrument);
                    sphereCollider.radius = radius;
                    sphereCollider.isTrigger = false; 
                }
                else
                {
                    childCollider.isTrigger = false;
                }
            }
            else
            {
                existingCollider.isTrigger = false;
                Debug.Log($"Using existing collider on {instrument.name}");
            }
            
            Collider[] allColliders = instrument.GetComponentsInChildren<Collider>();
        }
        
        void SetupInstrumentRendering(GameObject instrument)
        {
            Renderer[] renderers = instrument.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null && renderer.material != null)
                {
                    // Set render queue to ensure instruments render above planes
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
    



