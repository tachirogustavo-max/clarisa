namespace Clarisa.Application.Abstractions.Persistencia;

public enum ModoUnidadTrabajo
{
    Lectura = 1,
    Escritura = 2
}

public interface IFabricaUnidadTrabajo
{
    Task<IUnidadTrabajo> IniciarAsync(ModoUnidadTrabajo modo, CancellationToken ct);
}
