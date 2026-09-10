using UnityEngine;

public class SimulatedDrag : MonoBehaviour
{
    [Header("Movement Distances")]
    [Tooltip("How far to move left and right on the X axis")]
    public float distanceX = 0.15f;

    [Tooltip("How far to move up and down on the Y axis")]
    public float distanceY = 0.15f;

    [Header("Speed")]
    [Tooltip("Time (in seconds) to complete one full back-and-forth movement")]
    public float cycleDuration = 1.0f;

    [Header("Motion Style")]
    [Tooltip("If true, moves in a smooth oval. If false, moves diagonally.")]
    public bool circularMotion = true;

    private Vector3 startPosition;

    void Start()
    {
        // Cache the initial starting position so the object doesn't drift away
        startPosition = transform.position;
    }

    void Update()
    {
        // Prevent division by zero just in case cycleDuration is set to 0 in the Inspector
        if (cycleDuration <= 0f) return;

        // Calculate where we are in the 1-second cycle using time and Pi (for radians)
        float phase = (Time.time / cycleDuration) * Mathf.PI * 2f;

        // Calculate the X offset using Sine (easing in and out naturally)
        float offsetX = Mathf.Sin(phase) * distanceX;

        // Calculate the Y offset. Using Cosine offsets the timing, creating a beautiful circular/oval wobble!
        float offsetY = circularMotion ? Mathf.Cos(phase) * distanceY : Mathf.Sin(phase) * distanceY;

        // Apply the new coordinates relative to where the object started
        transform.position = startPosition + new Vector3(offsetX, offsetY, 0f);
    }
}
