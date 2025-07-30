using UnityEngine;

namespace MultiplayerShooter.Gameplay.Weapons
{
    /// <summary>
    /// Handles weapon visual and audio effects
    /// </summary>
    public class WeaponEffectsSystem : MonoBehaviour
    {
        private PlayerCameraController m_CameraController;

        [Header("Visual Effects")]
        [SerializeField] private GameObject m_MuzzleFlashPrefab;
        [SerializeField] private GameObject m_ImpactEffectPrefab;
        [SerializeField] private Transform m_MuzzlePoint;

        [Header("Audio")]
        [SerializeField] private AudioSource m_AudioSource;
        [SerializeField] private AudioClip m_FireSound;
        [SerializeField] private AudioClip m_ReloadSound;
        private void Awake()
        {
            if (m_AudioSource == null)
                m_AudioSource = GetComponent<AudioSource>();

            m_CameraController = GetComponentInParent<PlayerCameraController>();
        }

        public void PlayFireEffects()
        {
            PlayMuzzleFlash();
            PlayFireSound();

            // Trigger camera shake
            m_CameraController?.TriggerCameraShake();
        }

        public void PlayImpactEffects(object[] parameters)
        {
            Vector3 position = (Vector3)parameters[0];
            Vector3 normal = (Vector3)parameters[1];
            PlayImpactEffects(position, normal);
        }

        public void PlayImpactEffects(Vector3 position, Vector3 normal)
        {
            if (m_ImpactEffectPrefab != null)
            {
                var impact = Instantiate(m_ImpactEffectPrefab, position, Quaternion.LookRotation(normal));
                Destroy(impact, 2f);
            }
        }

        public void PlayReloadEffects()
        {
            if (m_AudioSource != null && m_ReloadSound != null)
            {
                m_AudioSource.PlayOneShot(m_ReloadSound);
            }
        }

        private void PlayMuzzleFlash()
        {
            if (m_MuzzleFlashPrefab != null && m_MuzzlePoint != null)
            {
                var flash = Instantiate(m_MuzzleFlashPrefab, m_MuzzlePoint.position, m_MuzzlePoint.rotation);
                Destroy(flash, 0.1f);
            }
        }

        private void PlayFireSound()
        {
            if (m_AudioSource != null && m_FireSound != null)
            {
                m_AudioSource.PlayOneShot(m_FireSound);
            }
        }
    }
}