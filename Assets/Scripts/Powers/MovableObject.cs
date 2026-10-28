using System.Collections.Generic;
using UnityEngine;

namespace TiffanyGame
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class MovableObject : MonoBehaviour
    {
        public ThrowableKind kind;
        public float pushSpeed = 2.5f;
        public float liftSpeed = 2f;
        public float spin = 6f;
        public AudioSource sound;

        private static readonly List<MovableObject> active = new List<MovableObject>();
        private Rigidbody body;

        public static IReadOnlyList<MovableObject> Active => active;
        public Throwable Throwable { get; private set; }
        public bool Used => Throwable.Used;

        private void Awake()
        {
            Throwable = new Throwable(kind);
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
        }

        private void OnEnable() => active.Add(this);

        private void OnDisable() => active.Remove(this);

        public void Move(Vector3 from, float noiseRadius)
        {
            Vector3 direction = transform.position - from;
            direction.y = 0f;
            if (direction.sqrMagnitude == 0f) direction = transform.forward;
            direction.y = 0f;
            direction.Normalize();
            body.isKinematic = false;
            body.AddForce(direction * pushSpeed + Vector3.up * liftSpeed, ForceMode.VelocityChange);
            body.AddTorque(Vector3.Cross(Vector3.up, direction) * spin, ForceMode.VelocityChange);
            if (sound != null && sound.clip != null) sound.Play();
            NoiseBus.Publish(new StepNoise(transform.position, noiseRadius));
            Debug.Log($"{GameInfo.LogPrefix} Poltergeist: {kind} suena a {noiseRadius:0.#} m");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => active.Clear();
    }
}
