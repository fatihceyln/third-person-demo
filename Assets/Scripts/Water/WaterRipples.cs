using UnityEngine;

namespace ThirdPersonDemo.Gameplay
{
    // Feeds the player's position in the water to the water shader, which draws the foam around the legs and the ripple rings.
    public sealed class WaterRipples : MonoBehaviour
    {
        // Must match WATER_RIPPLE_COUNT in ToonWater.shader.
        private const int MaxRipples = 16;
        // Start time for empty slots: far enough in the past that the shader treats them as finished.
        private const float FinishedRippleTime = -1000f;

        private static readonly int RipplesId = Shader.PropertyToID("_WaterRipples");
        private static readonly int RippleSettingsId = Shader.PropertyToID("_WaterRippleSettings");
        private static readonly int ContactId = Shader.PropertyToID("_WaterContact");

        [SerializeField] private PlayerController playerController;
        [SerializeField, Min(0.05f)] private float rippleSpacing = 0.4f;
        [SerializeField, Min(0.1f)] private float idleRippleInterval = 1.2f;
        [SerializeField, Min(0.1f)] private float rippleLifetime = 1.6f;
        [SerializeField, Min(0f)] private float rippleSpeed = 0.7f;
        [SerializeField, Min(0.01f)] private float contactFadeSpeed = 4f;

        private readonly Vector4[] ripples = new Vector4[MaxRipples];
        private int nextRipple;
        private Vector3 lastRipplePosition;
        private float lastRippleTime;
        private float contact;

        private void Awake()
        {
            for (var index = 0; index < MaxRipples; index++)
            {
                ripples[index] = new Vector4(0f, 0f, FinishedRippleTime, 0f);
            }
        }

        private void Update()
        {
            var inWater = playerController.IsInWater;
            var position = transform.position;
            contact = Mathf.MoveTowards(contact, inWater ? 1f : 0f, contactFadeSpeed * Time.deltaTime);

            if (inWater)
            {
                var moved = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(lastRipplePosition.x, lastRipplePosition.z));

                if (moved >= rippleSpacing || Time.time - lastRippleTime >= idleRippleInterval)
                {
                    AddRipple(position);
                }
            }

            // The shader ages ripples with this clock rather than its own, so both use the same time base.
            Shader.SetGlobalVectorArray(RipplesId, ripples);
            Shader.SetGlobalVector(RippleSettingsId, new Vector4(Time.time, rippleLifetime, rippleSpeed, 0f));
            Shader.SetGlobalVector(ContactId, new Vector4(position.x, position.z, contact, 0f));
        }

        private void OnDisable()
        {
            // Globals outlive Play Mode; a zero lifetime and contact keep the edit-mode water clean.
            Shader.SetGlobalVector(RippleSettingsId, Vector4.zero);
            Shader.SetGlobalVector(ContactId, Vector4.zero);
        }

        private void AddRipple(Vector3 position)
        {
            ripples[nextRipple] = new Vector4(position.x, position.z, Time.time, 0f);
            nextRipple = (nextRipple + 1) % MaxRipples;
            lastRipplePosition = position;
            lastRippleTime = Time.time;
        }
    }
}
