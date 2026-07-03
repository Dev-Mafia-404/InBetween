using UnityEngine;
using UnityEngine.Events;
public class StartEnemy : MonoBehaviour
{
    [SerializeField] private UnityEvent onEnemyStarted;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        onEnemyStarted?.Invoke();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
