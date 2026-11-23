using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace AsykAtu
{
    /// <summary>
    /// Шаг физики = частота шлема × multiplier. При 72 Гц сақа на 8 м/с за шаг пролетает 11 см,
    /// это больше самого асыка, поэтому шаг вдвое чаще кадров. Тел в сцене мало, Quest это тянет.
    /// </summary>
    public class PhysicsRateSync : MonoBehaviour
    {
        [Tooltip("Во сколько раз шаг физики чаще кадров шлема.")]
        public float multiplier = 2f;
        [Tooltip("Частота кадров, если шлема нет (эмулятор), Гц.")]
        public float fallbackRate = 72f;

        static readonly List<XRDisplaySubsystem> Displays = new List<XRDisplaySubsystem>();
        float appliedRate;

        void Update()
        {
            float hz = fallbackRate;
            Displays.Clear();
            SubsystemManager.GetSubsystems(Displays);
            foreach (var display in Displays)
            {
                if (display.running && display.TryGetDisplayRefreshRate(out float rate) && rate >= 30f)
                {
                    hz = rate;
                    break;
                }
            }

            float physicsRate = hz * multiplier;
            if (Mathf.Approximately(physicsRate, appliedRate)) return;
            appliedRate = physicsRate;
            Time.fixedDeltaTime = 1f / physicsRate;
            Debug.Log($"[AsykAtu] Шаг физики: {physicsRate:0} Гц (кадры {hz:0} Гц × {multiplier:0.#})");
        }
    }
}
