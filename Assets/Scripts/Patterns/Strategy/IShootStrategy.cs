/// <summary>
/// Estrategia de disparo. La torre arma un ShootContext (objetivo primario,
/// objetivos en rango, daño, acceso al pool) y la estrategia decide qué hacer con él.
/// </summary>
public interface IShootStrategy
{
    void Shoot(ShootContext ctx);
}
