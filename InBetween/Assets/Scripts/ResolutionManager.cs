using UnityEngine;

public class ResolutionManager : MonoBehaviour
{
    [Header("Target Resolution")]
    [SerializeField] private int width = 1920;
    [SerializeField] private int height = 1440;
    [SerializeField] private FullScreenMode fullScreenMode = FullScreenMode.FullScreenWindow;

    private void Awake()
    {
        Screen.SetResolution(width, height, fullScreenMode);    
    }
}