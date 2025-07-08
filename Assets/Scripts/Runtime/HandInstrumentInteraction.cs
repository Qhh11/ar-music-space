using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.XR.ARFoundation.Samples
{

    public class HandInstrumentInteraction : MonoBehaviour
    {
        [Header("Hand Detection")]
        [SerializeField]
        private Client handMeshClient;

        [SerializeField]
        private LayerMask instrumentLayerMask = 1;

        [Header("Interaction Settings")]
        [SerializeField]
        private float hitCooldown = 0.2f;

        [SerializeField]
        private float interactionDistance = 0.2f; 

        [SerializeField]
        private bool useCollisionDetection = true;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Show debug information")]
        private bool enableDebugLogs = true;

        [SerializeField]
        [Tooltip("Visualize interaction zones")]
        private bool showDebugGizmos = false;

        // Private state
        private GameObject currentHandMesh;
        private Dictionary<GameObject, float> lastHitTimes = new Dictionary<GameObject, float>();
        private List<GameObject> instrumentsInRange = new List<GameObject>();
        private Vector3 lasthandPosition;
        private bool handWasPresent = false;
        
        private SphereCollider handCollider;

    void Start()
        {
            InitializeHandDetection();
        }

        void Update()
        {
            UpdateHandMesh();
            
            if (currentHandMesh != null)
            {
                if (useCollisionDetection)
                {
                    UpdateCollisionDetection();
                }
                else
                {
                    UpdateDistanceDetection();
                }
            }
        }
        
        void InitializeHandDetection()
        {
            if (handMeshClient == null)
            {
                handMeshClient = FindObjectOfType<Client>();
                if (handMeshClient == null)
                {
                    LogError("No Client script found! Please assign the hand mesh client.");
                }
            }
            
        }
        
        void UpdateHandMesh()
        {
            if (handMeshClient == null) return;

            GameObject newHandMesh = handMeshClient.getHandmesh();
            
            if (newHandMesh != currentHandMesh)
            {
                if (currentHandMesh != null)
                {
                    CleanupHandCollider();
                    handWasPresent = false;
                }

                currentHandMesh = newHandMesh;

                if (currentHandMesh != null)
                {
                    SetupHandCollider();
                    handWasPresent = true;
                    LogDebug("New hand mesh detected, collision setup complete");
                }
            }

            
            if (currentHandMesh != null)
            {
                Vector3 currentPosition = currentHandMesh.transform.position;
                
                if (Vector3.Distance(currentPosition, lasthandPosition) > 0.01f)
                {
                    lasthandPosition = currentPosition;
                }
            }
        }

      
        void SetupHandCollider()
        {
            if (currentHandMesh == null) return;
            
            handCollider = currentHandMesh.GetComponent<SphereCollider>();
            if (handCollider == null)
            {
                handCollider = currentHandMesh.AddComponent<SphereCollider>();
            }
            
            handCollider.isTrigger = true;
            handCollider.radius = interactionDistance;
            
            Rigidbody handRigidbody = currentHandMesh.GetComponent<Rigidbody>();
            if (handRigidbody == null)
            {
                handRigidbody = currentHandMesh.AddComponent<Rigidbody>();
                handRigidbody.isKinematic = true; 
                handRigidbody.useGravity = false;
            }

            LogDebug($"Hand collider setup: radius {handCollider.radius}");
        }

       
        void CleanupHandCollider()
        {
            if (handCollider != null)
            {
                Destroy(handCollider);
                handCollider = null;
            }

            instrumentsInRange.Clear();
        }

     
        void UpdateCollisionDetection()
        {
            if (currentHandMesh == null || handCollider == null) return;

            Vector3 handPosition = currentHandMesh.transform.position;
            if (currentHandMesh.transform.parent != null)
            {
                Camera cam = currentHandMesh.transform.parent.GetComponent<Camera>();
                if (cam != null && Vector3.Distance(handPosition, cam.transform.position) < 0.1f)
                {
                    handPosition = cam.transform.position + cam.transform.forward * 0.3f;
                    LogDebug($"Corrected hand position from camera center to: {handPosition}");
                }
            }
            
            if (Time.frameCount % 60 == 0) 
            {
                LogDebug($"Hand position: {handPosition}");
            }
            
            Collider[] nearbyColliders = Physics.OverlapSphere(
                handPosition,  
                interactionDistance, 
                instrumentLayerMask
            );

            LogDebug($"OverlapSphere found {nearbyColliders.Length} colliders at position {handPosition} with radius {interactionDistance}");
            
            DrumHitHandler[] allInstruments = FindObjectsOfType<DrumHitHandler>();
            LogDebug($"Total instruments in scene: {allInstruments.Length}");
            
            foreach (DrumHitHandler instrument in allInstruments)
            {
                if (instrument != null)
                {
                    Vector3 instrumentPos = instrument.transform.position;
                    float distance = Vector3.Distance(handPosition, instrumentPos);
                    
                    LogDebug($"Instrument {instrument.name}: position {instrumentPos}, distance from hand: {distance:F3}m");
                    
                    if (distance <= interactionDistance)
                    {
                        LogDebug($"Instrument {instrument.name} is within interaction distance but not detected by OverlapSphere!");
                        
                        TryHitInstrument(instrument.gameObject, handPosition);
                    }
                }
            }

            foreach (Collider collider in nearbyColliders)
            {
                GameObject instrument = collider.gameObject;
                LogDebug($"OverlapSphere detected collider: {collider.name} on GameObject: {instrument.name}");
                TryHitInstrument(instrument, handPosition);
            }
        }
        
        void UpdateDistanceDetection()
        {
            if (currentHandMesh == null) return;
            
            DrumHitHandler[] allInstruments = FindObjectsOfType<DrumHitHandler>();
            
            foreach (DrumHitHandler instrument in allInstruments)
            {
                float distance = Vector3.Distance(
                    currentHandMesh.transform.position, 
                    instrument.transform.position
                );

                if (distance <= interactionDistance)
                {
                    TryHitInstrument(instrument.gameObject, currentHandMesh.transform.position);
                }
            }
        }
        
        void TryHitInstrument(GameObject instrument, Vector3 hitPosition)
        {
            if (instrument == null) return;

            LogDebug($"TryHitInstrument called for: {instrument.name} at position {hitPosition}");

            // Check cooldown
            if (lastHitTimes.ContainsKey(instrument))
            {
                float timeSinceLastHit = Time.time - lastHitTimes[instrument];
                if (timeSinceLastHit < hitCooldown)
                {
                    LogDebug($"Instrument {instrument.name} still in cooldown: {timeSinceLastHit:F2}s < {hitCooldown}s");
                    return; 
                }
            }
            
            DrumHitHandler drumHandler = instrument.GetComponent<DrumHitHandler>();
            if (drumHandler == null)
            {
                drumHandler = instrument.GetComponentInChildren<DrumHitHandler>();
            }

            if (drumHandler != null)
            {
                drumHandler.OnDrumHit(hitPosition);
                
                lastHitTimes[instrument] = Time.time;
                
                LogDebug($"SUCCESS: Hand hit instrument: {instrument.name} at position {hitPosition}");
            }
            else
            {
                LogDebug($"ERROR: No DrumHitHandler found on {instrument.name}");
            }
        }

      
        void OnTriggerEnter(Collider other)
        {
            if (!useCollisionDetection) return;
            
            if (IsInstrument(other.gameObject))
            {
                instrumentsInRange.Add(other.gameObject);
                LogDebug($"Hand entered instrument range: {other.gameObject.name}");
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (!useCollisionDetection) return;
            
            if (IsInstrument(other.gameObject))
            {
                instrumentsInRange.Remove(other.gameObject);
                LogDebug($"Hand left instrument range: {other.gameObject.name}");
            }
        }

        void OnTriggerStay(Collider other)
        {
            if (!useCollisionDetection) return;
            
            if (IsInstrument(other.gameObject))
            {
                TryHitInstrument(other.gameObject, currentHandMesh.transform.position);
            }
        }

 
        bool IsInstrument(GameObject obj)
        {
           
            return ((1 << obj.layer) & instrumentLayerMask) != 0 || 
                   obj.GetComponent<DrumHitHandler>() != null ||
                   obj.GetComponentInChildren<DrumHitHandler>() != null;
        }

        void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;

            if (currentHandMesh != null)
            {
                // Draw interaction sphere around hand
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(currentHandMesh.transform.position, interactionDistance);

                // Draw line to nearby instruments
                Gizmos.color = Color.red;
                foreach (GameObject instrument in instrumentsInRange)
                {
                    if (instrument != null)
                    {
                        Gizmos.DrawLine(currentHandMesh.transform.position, instrument.transform.position);
                    }
                }
            }
        }
        
        void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[HandInstrumentInteraction] {message}");
            }
        }

        void LogError(string message)
        {
            Debug.LogError($"[HandInstrumentInteraction] {message}");
        }

    }
}