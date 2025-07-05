using System.Collections;
using UnityEngine;

namespace UnityEngine.XR.ARFoundation.Samples
{
    /// <summary>
/// Handles drum hit effects including audio, visual feedback, and particle effects
/// </summary>
public class DrumHitHandler : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField]
    [Tooltip("Audio clip to play when drum is hit")]
    AudioClip m_DrumSound;

    [SerializeField]
    [Tooltip("Volume of the drum hit sound")]
    [Range(0f, 1f)]
    float m_Volume = 0.8f;

    [Header("Visual Effects")]
    [SerializeField]
    [Tooltip("Particle system to play on drum hit")]
    ParticleSystem m_HitEffect;

    [SerializeField]
    [Tooltip("Color to flash when drum is hit")]
    Color m_HitColor = Color.red;

    [SerializeField]
    [Tooltip("Duration of the hit visual effect")]
    float m_HitEffectDuration = 0.1f;

    [Header("Physics")]
    [SerializeField]
    [Tooltip("Force applied to the drum when hit")]
    float m_HitForce = 5f;

    [SerializeField]
    [Tooltip("Enable physics response on hit")]
    bool m_EnablePhysicsResponse = true;

    // Private components
    AudioSource m_AudioSource;
    Renderer m_DrumRenderer;
    Rigidbody m_Rigidbody;
    Color m_OriginalColor;
    Material m_DrumMaterial;

    // State tracking
    bool m_IsFlashing = false;

    /// <summary>
    /// Audio clip played when drum is hit
    /// </summary>
    public AudioClip drumSound
    {
        get => m_DrumSound;
        set => m_DrumSound = value;
    }

    /// <summary>
    /// Particle effect played when drum is hit
    /// </summary>
    public ParticleSystem hitEffect
    {
        get => m_HitEffect;
        set => m_HitEffect = value;
    }

    void Start()
    {
        InitializeComponents();
    }


    void InitializeComponents()
    {
        // Setup audio source
        m_AudioSource = GetComponent<AudioSource>();
        if (m_AudioSource == null)
        {
            m_AudioSource = gameObject.AddComponent<AudioSource>();
        }
        
        m_AudioSource.playOnAwake = false;
        m_AudioSource.volume = m_Volume;
        
        m_DrumRenderer = GetComponentInChildren<Renderer>();
        if (m_DrumRenderer != null)
        {
            m_DrumMaterial = m_DrumRenderer.material;
            m_OriginalColor = m_DrumMaterial.color;
        }
        else
        {
            Debug.LogWarning($"No Renderer found on {gameObject.name} or its children. Visual effects will not work.", this);
        }

        // Get rigidbody for physics effects
        m_Rigidbody = GetComponent<Rigidbody>();
        if (m_Rigidbody == null && m_EnablePhysicsResponse)
        {
            Debug.LogWarning($"No Rigidbody found on {gameObject.name}. Physics effects will not work.", this);
        }

        // If no drum sound is assigned, try to load a default one
        if (m_DrumSound == null)
        {
            Debug.Log($"No drum sound assigned to {gameObject.name}. Add an audio clip for sound effects.");
        }
    }


    public void OnDrumHit()
    {
        Debug.Log($"Drum hit: {gameObject.name}");
        
        // Play audio effect
        PlayDrumSound();
        
        // Play visual effect
        if (!m_IsFlashing)
        {
            StartCoroutine(HitVisualEffect());
        }
        
        // Play particle effect
        PlayParticleEffect();
        
        // Apply physics effect
        ApplyPhysicsEffect();
    }


    public void OnDrumHit(Vector3 hitPoint)
    {
        OnDrumHit();
        
        // Apply localized physics effect if rigidbody exists
        if (m_Rigidbody != null && m_EnablePhysicsResponse)
        {
            Vector3 direction = (hitPoint - transform.position).normalized;
            m_Rigidbody.AddForceAtPosition(direction * m_HitForce, hitPoint, ForceMode.Impulse);
        }
    }


    void PlayDrumSound()
    {
        if (m_AudioSource != null && m_DrumSound != null)
        {
            m_AudioSource.PlayOneShot(m_DrumSound, m_Volume);
        }
        else if (m_AudioSource != null)
        {
            // Play a simple beep sound as fallback
            Debug.Log($"Playing drum sound: BOOM! (No audio clip assigned)");
        }
    }


    void PlayParticleEffect()
    {
        if (m_HitEffect != null)
        {
            // Reset particle system if it was already playing
            if (m_HitEffect.isPlaying)
            {
                m_HitEffect.Stop();
            }
            
            m_HitEffect.Play();
        }
    }


    void ApplyPhysicsEffect()
    {
        if (m_Rigidbody != null && m_EnablePhysicsResponse)
        {
            // Apply a small upward force to simulate drum bounce
            Vector3 bounceForce = Vector3.up * m_HitForce;
            m_Rigidbody.AddForce(bounceForce, ForceMode.Impulse);
        }
    }


    IEnumerator HitVisualEffect()
    {
        if (m_DrumRenderer == null || m_DrumMaterial == null)
            yield break;

        m_IsFlashing = true;

        // Change color to hit color
        m_DrumMaterial.color = m_HitColor;
        
        // Wait for effect duration
        yield return new WaitForSeconds(m_HitEffectDuration);
        
        // Restore original color
        m_DrumMaterial.color = m_OriginalColor;
        
        m_IsFlashing = false;
    }
    
    public void SetDrumSound(AudioClip newSound)
    {
        m_DrumSound = newSound;
    }


    public void SetHitEffect(ParticleSystem newEffect)
    {
        m_HitEffect = newEffect;
    }


    public void SetVolume(float newVolume)
    {
        m_Volume = Mathf.Clamp01(newVolume);
        if (m_AudioSource != null)
        {
            m_AudioSource.volume = m_Volume;
        }
    }


    public void SetPhysicsResponse(bool enable)
    {
        m_EnablePhysicsResponse = enable;
    }


    [ContextMenu("Test Drum Hit")]
    public void TestDrumHit()
    {
        OnDrumHit();
    }

    void OnValidate()
    {
        // Clamp volume in inspector
        m_Volume = Mathf.Clamp01(m_Volume);
        
        // Update audio source volume if it exists
        if (m_AudioSource != null)
        {
            m_AudioSource.volume = m_Volume;
        }
    }
}
}
