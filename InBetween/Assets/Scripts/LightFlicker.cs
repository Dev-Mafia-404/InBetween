using UnityEngine;

[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    [Header("Intensity")]
    [SerializeField] private float minIntensity = 0.8f;
    [SerializeField] private float maxIntensity = 2f;

    [Header("Flicker")]
    [SerializeField] private float flickerSpeed = 8f;
    [SerializeField] private float randomOffset = 100f;

    private Light lightSource;
    private float seed;

    private void Awake()
    {
        lightSource = GetComponent<Light>();
        seed = Random.Range(0f, randomOffset);
    }

    private void Update()
    {
        float noise = Mathf.PerlinNoise(
            seed,
            Time.time * flickerSpeed
        );

        lightSource.intensity = Mathf.Lerp(
            minIntensity,
            maxIntensity,
            noise
        );
    }
}