using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Auditoria;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Resultados;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Lapsos;

public sealed class GestionLapsos(IFabricaUnidadTrabajo fabrica, IReloj reloj)
{
    private const string Entidad = nameof(Lapso);

    public async Task<RegistroVersionado> CrearAsync(Actor actor, CrearLapso solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        var nuevo = Error.DesdeDominio(() =>
            new Lapso(datos.Id, datos.AnioLectivoId, datos.Nombre, datos.Orden, datos.Inicio, datos.Fin, version: 1));

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var existente = await unidad.Lapsos.ObtenerAsync(nuevo.Id, ct);
        if (existente is not null) return Reintentos.Resolver(existente.MismoContenido(nuevo), existente.Id, existente.Version);
        await ValidarEnAnioAsync(unidad, nuevo, null, ct);

        await unidad.Lapsos.AgregarAsync(nuevo, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Creacion, Entidad, nuevo.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(nuevo.Id, nuevo.Version);
    }

    public async Task<RegistroVersionado> ModificarAsync(Actor actor, ModificarLapso solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var lapso = await unidad.Lapsos.ObtenerAsync(datos.Id, ct) ?? throw Error.NoEncontrado("El momento no existe.");
        Error.ExigirVersion(lapso.Version, datos.VersionEsperada);
        Error.DesdeDominio(() => lapso.Modificar(datos.Nombre, datos.Orden, datos.Inicio, datos.Fin));
        await ValidarEnAnioAsync(unidad, lapso, lapso.Id, ct);

        var version = await unidad.Lapsos.ActualizarAsync(lapso, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Modificacion, Entidad, lapso.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(lapso.Id, version);
    }

    public async Task<IReadOnlyList<LapsoDatos>> ListarPorAnioAsync(Actor actor, Guid anioLectivoId, CancellationToken ct)
    {
        Autorizacion.ExigirPersonal(actor);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        var anio = await unidad.AniosLectivos.ObtenerAsync(anioLectivoId, ct) ?? throw Error.NoEncontrado("El año lectivo no existe.");
        var lapsos = await unidad.Lapsos.ListarPorAnioAsync(anioLectivoId, ct);
        return lapsos.OrderBy(lapso => lapso.Orden).Select(lapso => LapsoDatos.Desde(lapso, anio.LapsoActualId)).ToList();
    }

    // Marcar un lapso como actual es solo una referencia de trabajo: no crea ni abre su ventana de carga.
    public async Task<RegistroVersionado> SeleccionarActualAsync(Actor actor, SeleccionarLapsoActual solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var anio = await unidad.AniosLectivos.ObtenerAsync(datos.AnioLectivoId, ct)
            ?? throw Error.NoEncontrado("El año lectivo no existe.");
        Error.ExigirVersion(anio.Version, datos.VersionEsperada);
        if (datos.LapsoId is { } lapsoId)
        {
            var lapso = await unidad.Lapsos.ObtenerAsync(lapsoId, ct);
            if (lapso is null || lapso.AnioLectivoId != anio.Id)
                throw Error.ReferenciaInvalida("El momento seleccionado no pertenece a este año lectivo.", nameof(SeleccionarLapsoActual.LapsoId));
        }
        anio.SeleccionarLapsoActual(datos.LapsoId);

        var version = await unidad.AniosLectivos.ActualizarAsync(anio, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.SeleccionLapsoActual, nameof(AnioLectivo), anio.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(anio.Id, version);
    }

    private static async Task ValidarEnAnioAsync(IUnidadTrabajo unidad, Lapso lapso, Guid? excluirId, CancellationToken ct)
    {
        var anio = await unidad.AniosLectivos.ObtenerAsync(lapso.AnioLectivoId, ct)
            ?? throw Error.ReferenciaInvalida("El año lectivo seleccionado no existe.", nameof(Lapso.AnioLectivoId));
        if (!anio.Fechas.Contiene(lapso.Fechas))
            throw Error.DatosInvalidos("Las fechas del momento deben quedar dentro del año lectivo.", nameof(Lapso.Fechas));
        if (await unidad.Lapsos.ExisteOrdenAsync(lapso.AnioLectivoId, lapso.Orden, excluirId, ct))
            throw Error.Duplicado("Ya hay un momento con ese orden en el año lectivo.", nameof(Lapso.Orden));
        if (await unidad.Lapsos.ExisteNombreAsync(lapso.AnioLectivoId, lapso.Nombre, excluirId, ct))
            throw Error.Duplicado("Ya hay un momento con ese nombre en el año lectivo.", nameof(Lapso.Nombre));
    }
}
