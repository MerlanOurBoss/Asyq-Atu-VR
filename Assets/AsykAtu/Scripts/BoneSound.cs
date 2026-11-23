using UnityEngine;

namespace AsykAtu
{
    /// <summary>
    /// Звук удара кости: о другую кость — звонкий щелчок, о землю — глухой стук.
    /// Громкость зависит от скорости удара, высота чуть плавает, чтобы удары не звучали одинаково.
    /// Вешается на асык и сақа вместе с AudioSource.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class BoneSound : MonoBehaviour
    {
        public AudioClip[] boneClips;
        public AudioClip[] groundClips;
        [Tooltip("Медленнее этого удар не слышен, м/с.")]
        public float minSpeed = 0.2f;
        [Tooltip("С этой скорости удар звучит в полную громкость, м/с.")]
        public float fullSpeed = 4f;
        [Tooltip("Высота звука: больше 1 — звонче (мелкая кость), меньше — ниже (крупная).")]
        public float pitch = 1f;
        [Range(0f, 0.3f)] public float pitchJitter = 0.08f;
        [Tooltip("Не чаще одного звука за это время, с: иначе кость, которая подпрыгивает на месте, трещит.")]
        public float cooldown = 0.05f;

        /// <summary>Когда кость последний раз звучала (Time.time), −1 — ещё не звучала.</summary>
        public float LastPlayTime { get; private set; } = -1f;
        /// <summary>Последний звук — удар о кость (а не о землю).</summary>
        public bool LastHitBone { get; private set; }

        AudioSource source;
        float nextAllowed;

        void Awake() => source = GetComponent<AudioSource>();

        void OnCollisionEnter(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (speed < minSpeed || Time.time < nextAllowed) return;

            var other = collision.rigidbody != null ? collision.rigidbody.GetComponent<BoneSound>() : null;
            // Удар двух костей слышен один раз: звучит одна из двух.
            if (other != null && other.GetHashCode() < GetHashCode()) return;

            var clips = other != null ? boneClips : groundClips;
            if (clips == null || clips.Length == 0) return;

            float loudness = Mathf.InverseLerp(minSpeed, fullSpeed, speed);
            source.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
            source.PlayOneShot(clips[Random.Range(0, clips.Length)], 0.1f + 0.9f * loudness);
            LastPlayTime = Time.time;
            LastHitBone = other != null;
            nextAllowed = Time.time + cooldown;
        }
    }
}
