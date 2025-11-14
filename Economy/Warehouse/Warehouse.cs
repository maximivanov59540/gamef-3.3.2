using UnityEngine;
public class Warehouse : MonoBehaviour
{
    [Tooltip("На сколько этот склад увеличивает глобальный лимит хранения")]
    public float limitIncrease = 100f;

    void Start()
    {
        // При постройке - увеличиваем лимит
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.IncreaseGlobalLimit(limitIncrease);
        }
    }

    void OnDestroy()
    {
        // При сносе - уменьшаем лимит
        // (Проверяем Instance на случай, если выходим из игры)
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.IncreaseGlobalLimit(-limitIncrease);
        }
    }
}