using System.Collections.Generic;

namespace UnityEngine.XR.ARFoundation.Samples
{
    /// <summary>
    /// Subscribes to an <see cref="ARRaycastHitEventAsset"/>. When the event is raised, the
    /// <see cref="prefabToPlace"/> is instantiated at, or moved to, the hit position.
    /// </summary>
    public class ARPlaceObject : MonoBehaviour
    {
        const float k_PrefabHalfSize = 0.025f;

        [Header("Drum Configuration")]
        [SerializeField]
        [Tooltip("The drum prefab to be placed.")]
        GameObject m_DrumPrefab;
        
        [SerializeField]
        [Tooltip("Maximum number of drums allowed")]
        int m_MaxDrums = 5;
        
        [SerializeField]
        [Tooltip("Layer mask for drum objects")]
        LayerMask m_DrumLayerMask = 1;
        

        [Header("AR Events")]
        [SerializeField]
        [Tooltip("ScriptableObject containing ARRaycastHit events")]
        ARRaycastHitEventAsset m_RaycastHitEvent;

        List<GameObject> m_PlacedDrums = new List<GameObject>();
        GameObject m_SpawnedObject;
        


        /// <summary>
        /// The drum prefab to be placed
        /// </summary>
        public GameObject drumPrefab
        {
            get => m_DrumPrefab;
            set => m_DrumPrefab = value;
        }

        /// <summary>
        /// The spawned prefab instance.
        /// </summary>
        public GameObject spawnedObject
        {
            get => m_SpawnedObject;
            set => m_SpawnedObject = value;
        }

        void OnEnable()
        {
            if (m_RaycastHitEvent == null || m_DrumPrefab == null)
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

        void PlaceObjectAt(object sender, ARRaycastHit hitPose)
        {
            if (CheckForDrumHit(hitPose))
                return;
            if (m_PlacedDrums.Count >= m_MaxDrums)
            {
                Debug.Log($"Reached maximum drum limit: {m_MaxDrums}");
                return;
            }
            
            GameObject newDrum = Instantiate(m_DrumPrefab, hitPose.trackable.transform.parent);

            // Position the drum
            var forward = hitPose.pose.rotation * Vector3.up;
            var offset = forward * k_PrefabHalfSize;
            newDrum.transform.position = hitPose.pose.position + offset;
            newDrum.transform.rotation = hitPose.pose.rotation;
            newDrum.transform.parent = hitPose.trackable.transform.parent;

            SetupDrum(newDrum);
            
            m_PlacedDrums.Add(newDrum);
            m_SpawnedObject = newDrum;
            
            Debug.Log($"Placed drum #{m_PlacedDrums.Count} at position: {newDrum.transform.position}");
        }
        
        /// <summary>
        /// Check if the raycast hit an existing drum for interaction
        /// </summary>
        bool CheckForDrumHit(ARRaycastHit hitPose)
        {
            // Cast a ray from the hit position upward to check for drums
            Vector3 rayStart = hitPose.pose.position;
            Vector3 rayDirection = Vector3.up;
            float rayDistance = 1f; // Check 1 meter above the hit point

            RaycastHit hit;
            if (Physics.Raycast(rayStart, rayDirection, out hit, rayDistance, m_DrumLayerMask))
            {
                DrumHitHandler drumHit = hit.collider.GetComponent<DrumHitHandler>();
                if (drumHit != null)
                {
                    drumHit.OnDrumHit();
                    Debug.Log($"Hit drum: {hit.collider.name}");
                    return true;
                }
            }

            // Also check at the exact hit position in case drum is on the plane
            Collider[] colliders = Physics.OverlapSphere(hitPose.pose.position, 0.2f, m_DrumLayerMask);
            foreach (var collider in colliders)
            {
                DrumHitHandler drumHit = collider.GetComponent<DrumHitHandler>();
                if (drumHit != null)
                {
                    drumHit.OnDrumHit();
                    Debug.Log($"Hit drum: {collider.name}");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Setup drum components after instantiation
        /// </summary>
        void SetupDrum(GameObject drum)
        {
            // Set layer for drum interaction
            SetLayerRecursively(drum, LayerMask.NameToLayer("Drum"));

            // Ensure collider exists
            if (drum.GetComponent<Collider>() == null)
            {
                CapsuleCollider collider = drum.AddComponent<CapsuleCollider>();
                collider.height = 0.3f;
                collider.radius = 0.15f;
            }

            // Ensure drum hit handler exists
            if (drum.GetComponent<DrumHitHandler>() == null)
            {
                drum.AddComponent<DrumHitHandler>();
            }

            // Set tag
            drum.tag = "Drum";
        }

        /// <summary>
        /// Recursively set layer for GameObject and its children
        /// </summary>
        void SetLayerRecursively(GameObject obj, int layer)
        {
            if (layer == -1) layer = 0; // Default layer

            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        /// <summary>
        /// Clear all placed drums
        /// </summary>
        public void ClearAllDrums()
        {
            foreach (GameObject drum in m_PlacedDrums)
            {
                if (drum != null)
                {
                    Destroy(drum);
                }
            }
            m_PlacedDrums.Clear();
            m_SpawnedObject = null;
            Debug.Log("Cleared all drums");
        }

        /// <summary>
        /// Get all placed drums
        /// </summary>
        public List<GameObject> GetAllDrums()
        {
            return new List<GameObject>(m_PlacedDrums);
        }
    }
}

