using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.XR.ARFoundation.Samples
{

    public class HandInstrumentInteraction : MonoBehaviour
    {
        [Header("Hand Detection")]
        [SerializeField]
        [Tooltip("Reference to the Client script that provides hand mesh")]
        private Client handMeshClient;

        [SerializeField]
        [Tooltip("Layer mask for instruments that can be hit")]
        private LayerMask instrumentLayerMask = 1;

        [Header("Interaction Settings")]
        [SerializeField]
        [Tooltip("Minimum time between hits on the same instrument")]
        private float hitCooldown = 0.2f;

        [SerializeField]
        [Tooltip("Distance threshold for hand-instrument interaction")]
        private float interactionDistance = 0.05f; // 5cm

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
        private Vector3 lastHandPosition;
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

        /// <summary>
        /// Initialize hand detection system
        /// </summary>
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

            LogDebug("Hand-Instrument Interaction initialized");
        }

        /// <summary>
        /// Update hand mesh reference and setup collision detection
        /// </summary>
        void UpdateHandMesh()
        {
            if (handMeshClient == null) return;

            GameObject newHandMesh = handMeshClient.getHandmesh();
            
            if (newHandMesh != currentHandMesh)
            {
                // Hand mesh changed (new hand detected or hand lost)
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

            // Update hand position for movement detection
            if (currentHandMesh != null)
            {
                Vector3 currentPosition = currentHandMesh.transform.position;
                
                // Detect significant hand movement for better hit detection
                if (Vector3.Distance(currentPosition, lastHandPosition) > 0.01f)
                {
                    lastHandPosition = currentPosition;
                }
            }
        }

        /// <summary>
        /// Setup collision detection for the hand mesh
        /// </summary>
        void SetupHandCollider()
        {
            if (currentHandMesh == null) return;

            // Add a sphere collider to the hand mesh for interaction detection
            handCollider = currentHandMesh.GetComponent<SphereCollider>();
            if (handCollider == null)
            {
                handCollider = currentHandMesh.AddComponent<SphereCollider>();
            }

            // Configure the collider
            handCollider.isTrigger = true;
            handCollider.radius = interactionDistance;

            // Add rigidbody if needed for proper collision detection
            Rigidbody handRigidbody = currentHandMesh.GetComponent<Rigidbody>();
            if (handRigidbody == null)
            {
                handRigidbody = currentHandMesh.AddComponent<Rigidbody>();
                handRigidbody.isKinematic = true; // Kinematic since hand is tracked, not physics-driven
                handRigidbody.useGravity = false;
            }

            LogDebug($"Hand collider setup: radius {handCollider.radius}");
        }

        /// <summary>
        /// Clean up hand collider when hand is lost
        /// </summary>
        void CleanupHandCollider()
        {
            if (handCollider != null)
            {
                Destroy(handCollider);
                handCollider = null;
            }

            // Clear instruments in range
            instrumentsInRange.Clear();
        }

        /// <summary>
        /// Update collision-based detection
        /// </summary>
        void UpdateCollisionDetection()
        {
            if (currentHandMesh == null || handCollider == null) return;

            Vector3 handPosition = currentHandMesh.transform.position;
            
            // Debug: Log hand position every few frames
            if (Time.frameCount % 60 == 0) // Every 60 frames (~1 second at 60fps)
            {
                LogDebug($"Hand position: {handPosition}");
            }

            // Use OverlapSphere to detect nearby instruments
            Collider[] nearbyColliders = Physics.OverlapSphere(
                handPosition, 
                interactionDistance, 
                instrumentLayerMask
            );

            LogDebug($"OverlapSphere found {nearbyColliders.Length} colliders at position {handPosition} with radius {interactionDistance}");

            // Debug: Also manually check all instruments in scene
            DrumHitHandler[] allInstruments = FindObjectsOfType<DrumHitHandler>();
            LogDebug($"Total instruments in scene: {allInstruments.Length}");
            
            foreach (DrumHitHandler instrument in allInstruments)
            {
                if (instrument != null)
                {
                    Vector3 instrumentPos = instrument.transform.position;
                    float distance = Vector3.Distance(handPosition, instrumentPos);
                    
                    LogDebug($"Instrument {instrument.name}: position {instrumentPos}, distance from hand: {distance:F3}m");
                    
                    // Check if it should be detected
                    if (distance <= interactionDistance)
                    {
                        LogDebug($"Instrument {instrument.name} is within interaction distance but not detected by OverlapSphere!");
                        
                        // Try direct hit as fallback
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

        /// <summary>
        /// Update distance-based detection (fallback method)
        /// </summary>
        void UpdateDistanceDetection()
        {
            if (currentHandMesh == null) return;

            // Find all instruments in the scene and check distance
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

        /// <summary>
        /// Attempt to hit an instrument
        /// </summary>
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
                    return; // Still in cooldown
                }
            }

            // Get the drum hit handler
            DrumHitHandler drumHandler = instrument.GetComponent<DrumHitHandler>();
            if (drumHandler == null)
            {
                drumHandler = instrument.GetComponentInChildren<DrumHitHandler>();
            }

            if (drumHandler != null)
            {
                // Trigger the drum hit
                drumHandler.OnDrumHit(hitPosition);
                
                // Update cooldown
                lastHitTimes[instrument] = Time.time;
                
                LogDebug($"SUCCESS: Hand hit instrument: {instrument.name} at position {hitPosition}");
            }
            else
            {
                LogDebug($"ERROR: No DrumHitHandler found on {instrument.name}");
            }
        }

        /// <summary>
        /// Unity collision detection callbacks (if using collision detection)
        /// </summary>
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
                // Continuous contact - could trigger based on movement or other criteria
                TryHitInstrument(other.gameObject, currentHandMesh.transform.position);
            }
        }

        /// <summary>
        /// Check if a GameObject is an instrument
        /// </summary>
        bool IsInstrument(GameObject obj)
        {
            // Check if object is on the instrument layer
            return ((1 << obj.layer) & instrumentLayerMask) != 0 || 
                   obj.GetComponent<DrumHitHandler>() != null ||
                   obj.GetComponentInChildren<DrumHitHandler>() != null;
        }

        /// <summary>
        /// Debug visualization
        /// </summary>
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

        

        #region Debug Helpers

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

        #endregion
    }
}