using UnityEngine;

public static class Physics2DLayerCollisionRules
{
    private const string PlayerLayerName = "Player";
    private const string EnemyLayerName = "Enemy";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        IgnoreCollision(PlayerLayerName, EnemyLayerName, true);
    }

    private static void IgnoreCollision(string firstLayerName, string secondLayerName, bool ignore)
    {
        int firstLayer = LayerMask.NameToLayer(firstLayerName);
        int secondLayer = LayerMask.NameToLayer(secondLayerName);

        if (firstLayer < 0 || secondLayer < 0)
        {
            return;
        }

        Physics2D.IgnoreLayerCollision(firstLayer, secondLayer, ignore);
    }
}
