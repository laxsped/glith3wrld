using UnityEngine;

/// <summary>
/// Вешается на тот же объект, что и Renderer с материалом ProximityGlitch.
/// Передаёт позицию игрока в шейдер каждый кадр.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class ProximityGlitchController : MonoBehaviour
{
    [Header("Player")]
    public Transform player;

    private Material _mat;
    private static readonly int PlayerWorldPos = Shader.PropertyToID("_PlayerWorldPos");

    private void Awake()
    {
        _mat = GetComponent<Renderer>().material;
    }

    private void Update()
    {
        if (player == null || _mat == null) return;
        _mat.SetVector(PlayerWorldPos, player.position);
    }
}
