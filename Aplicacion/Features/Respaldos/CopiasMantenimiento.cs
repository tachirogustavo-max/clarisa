using Clarisa.Application.Abstractions;
using Clarisa.Application.Common.Errores;

namespace Clarisa.Application.Features.Respaldos;

public sealed class CopiasMantenimiento(IRegistroOperacionesRespaldo registro, IGeneradorCopias generador,
    IAlmacenCopias almacen, IClaveRespaldos claves, IExclusionInstancia exclusion, IReloj reloj)
{
    public async Task<OperacionRespaldo> CrearAsync(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) throw Error.DatosInvalidos("Indica el identificador de la copia.", "Operacion");
        using var turno = exclusion.Adquirir();
        var anterior = await registro.ObtenerAsync(id, ct);
        if (anterior is not null)
        {
            if (anterior.Tipo != TipoOperacionRespaldo.Respaldo || anterior.Bytes is not { } bytes || anterior.Huella is not { } huella
                || !await almacen.PublicadaCoincideAsync(id, bytes, huella, ct))
                throw Error.EstadoNoPermitido("Ese intento no tiene una copia publicada verificable. Revísalo antes de continuar.");
            return await CompletarAsync(anterior);
        }
        if (!await claves.EstaPreparadaAsync(ct)) throw Error.EstadoNoPermitido("Prepare la clave de recuperación antes de actualizar.");
        if (!almacen.HayEspacioSuficiente()) throw Error.EstadoNoPermitido("No hay espacio suficiente para la copia previa.");
        var instante = reloj.Ahora;
        var (operacion, creada) = await registro.CrearSiNoExisteAsync(new(id, TipoOperacionRespaldo.Respaldo,
            EstadoOperacionRespaldo.EnCurso, FaseOperacionRespaldo.Generando, "Copia local previa a mantenimiento.",
            Guid.Empty, instante, instante), ct);
        if (!creada) throw Error.EstadoNoPermitido("El identificador ya está en uso; comprueba el resultado antes de repetir.");
        try
        {
            var copia = await generador.PrepararAsync(id, instante, ct);
            operacion = operacion with { Fase = FaseOperacionRespaldo.Publicando, Bytes = copia.Bytes, Huella = copia.Huella,
                Nombre = EjecucionRespaldos.NombreArchivo(id, instante), InstanteCopia = instante, ActualizadaUtc = reloj.Ahora };
            await registro.GuardarAsync(operacion, CancellationToken.None);
            await almacen.PublicarAsync(id, CancellationToken.None);
            if (!await almacen.PublicadaCoincideAsync(id, copia.Bytes, copia.Huella, CancellationToken.None))
                throw Error.EstadoNoPermitido("La copia publicada no coincide con la verificada. No continúe la actualización.");
            return await CompletarAsync(operacion);
        }
        catch
        {
            almacen.DescartarTemporal(id);
            throw;
        }
    }

    private async Task<OperacionRespaldo> CompletarAsync(OperacionRespaldo operacion)
    {
        if (operacion.Estado == EstadoOperacionRespaldo.Completa) return operacion;
        operacion = operacion with { Estado = EstadoOperacionRespaldo.Completa, Fase = FaseOperacionRespaldo.Terminada,
            Mensaje = "Copia local cifrada y verificada para mantenimiento.", ActualizadaUtc = reloj.Ahora };
        await registro.GuardarAsync(operacion, CancellationToken.None);
        return operacion;
    }
}
