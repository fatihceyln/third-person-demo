using UnityEngine;

namespace ThirdPersonDemo.Gameplay
{
    // Emits the foam bits that break off around the legs, the V-shaped wake and the spray droplets while the player wades.
    public sealed class WaterSplashes : MonoBehaviour
    {
        // Lifts the flat foam just above the surface so it does not flicker against the water.
        private const float SurfaceOffset = 0.01f;

        [SerializeField] private PlayerController playerController;
        [SerializeField] private ParticleSystem foamParticles;
        [SerializeField] private ParticleSystem sprayParticles;
        [SerializeField, Range(0f, 1f)] private float variation = 0.4f;

        [Header("Foam Around The Legs")]
        [SerializeField, Min(0f)] private float foamRateIdle = 14f;
        [SerializeField, Min(0f)] private float foamRateMoving = 45f;
        [SerializeField, Min(0f)] private float foamSpawnRadius = 0.22f;
        [SerializeField, Min(0f)] private float foamSpeed = 0.25f;
        [SerializeField, Min(0.05f)] private float foamLifetime = 1.1f;
        [SerializeField, Min(0.01f)] private float foamSize = 0.14f;

        [Header("Wake")]
        [SerializeField, Min(0f)] private float wakeRate = 45f;
        [SerializeField, Min(0f)] private float wakeSideSpeed = 0.45f;
        [SerializeField, Min(0.05f)] private float wakeLifetime = 1.6f;
        [SerializeField, Min(0.01f)] private float wakeSize = 0.15f;

        [Header("Spray")]
        [SerializeField, Min(0f)] private float minSprayHorizontalSpeed = 1f;
        [SerializeField, Min(0.05f)] private float sprayInterval = 0.22f;
        [SerializeField, Min(0)] private int sprayCount = 5;
        [SerializeField, Min(0)] private int entrySprayCount = 18;
        [SerializeField, Min(0f)] private float sprayUpSpeed = 1.6f;
        [SerializeField, Min(0f)] private float sprayOutSpeed = 0.6f;
        [SerializeField, Min(0.005f)] private float spraySize = 0.045f;

        private Vector3 lastPosition;
        private bool wasInWater;
        private float foamBudget;
        private float wakeBudget;
        private float sprayTimer;

        private void Start()
        {
            lastPosition = transform.position;
        }

        private void Update()
        {
            var position = transform.position;
            var flatVelocity = position - lastPosition;
            flatVelocity.y = 0f;
            flatVelocity /= Mathf.Max(Time.deltaTime, 0.0001f);
            lastPosition = position;

            if (!playerController.IsInWater)
            {
                wasInWater = false;
                return;
            }

            var center = new Vector3(position.x, playerController.WaterSurfaceHeight + SurfaceOffset, position.z);
            var speed = flatVelocity.magnitude;
            // Movement input, not the measured speed, so pushing against the pond wall still counts as walking.
            var moving = playerController.HorizontalSpeed > 0f;
            var direction = speed > 0.01f ? flatVelocity / speed : transform.forward;

            if (!wasInWater)
            {
                EmitSpray(center, direction, flatVelocity, entrySprayCount, true);
                wasInWater = true;
            }

            foamBudget += (moving ? foamRateMoving : foamRateIdle) * Time.deltaTime;

            while (foamBudget >= 1f)
            {
                foamBudget -= 1f;
                EmitFoam(center, flatVelocity);
            }

            if (moving)
            {
                wakeBudget += wakeRate * Time.deltaTime;

                while (wakeBudget >= 1f)
                {
                    wakeBudget -= 1f;
                    EmitWake(center, direction);
                }
            }

            sprayTimer += Time.deltaTime;

            if (speed >= minSprayHorizontalSpeed && sprayTimer >= sprayInterval)
            {
                sprayTimer = 0f;
                EmitSpray(center, direction, flatVelocity, sprayCount, false);
            }
        }

        private void EmitFoam(Vector3 center, Vector3 flatVelocity)
        {
            var outward = RandomFlatDirection();
            // The bits trail a little behind a walking player instead of keeping pace with the legs.
            var velocity = outward * Vary(foamSpeed) - flatVelocity * 0.15f;
            Emit(foamParticles, center + outward * foamSpawnRadius, velocity, Vary(foamLifetime), Vary(foamSize));
        }

        private void EmitWake(Vector3 center, Vector3 direction)
        {
            var side = Random.value < 0.5f ? -1f : 1f;
            var sideDirection = Vector3.Cross(Vector3.up, direction) * side;
            // A share of the bits stays on the center line so the V has a spine.
            var onCenterLine = Random.value < 0.25f;
            var spawn = center - direction * 0.2f + (onCenterLine ? Vector3.zero : sideDirection * 0.12f);
            var velocity = (onCenterLine ? Vector3.zero : sideDirection * Vary(wakeSideSpeed)) - direction * 0.1f;
            Emit(foamParticles, spawn, velocity, Vary(wakeLifetime), Vary(onCenterLine ? wakeSize * 0.7f : wakeSize));
        }

        private void EmitSpray(Vector3 center, Vector3 direction, Vector3 flatVelocity, int count, bool allAround)
        {
            var gravity = Physics.gravity.magnitude * sprayParticles.main.gravityModifier.constant;

            for (var index = 0; index < count; index++)
            {
                // Steps kick water backwards; landing in the water splashes it all around.
                var outward = allAround ? RandomFlatDirection() : Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * -direction;
                var upSpeed = Vary(sprayUpSpeed);
                var velocity = outward * Vary(sprayOutSpeed) + Vector3.up * upSpeed + flatVelocity * 0.3f;
                // Dies when it falls back to the surface, so the splash ring sub-emitter fires on the water.
                var lifetime = 2f * upSpeed / gravity;
                Emit(sprayParticles, center + outward * 0.12f, velocity, lifetime, Vary(spraySize));
            }
        }

        private void Emit(ParticleSystem particles, Vector3 position, Vector3 velocity, float lifetime, float size)
        {
            var emitParams = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startLifetime = lifetime,
                startSize = size,
                applyShapeToPosition = false,
            };
            particles.Emit(emitParams, 1);
        }

        private float Vary(float value)
        {
            return value * (1f + Random.Range(-variation, variation));
        }

        private static Vector3 RandomFlatDirection()
        {
            var angle = Random.value * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }
    }
}
