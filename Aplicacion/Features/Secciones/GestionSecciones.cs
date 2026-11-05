using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Auditoria;
using Clarisa.Application.Common.Consultas;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Resultados;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Domain.Entities;
using Clarisa.Domain.Common;
using Clarisa.Application.Features.Grados;

namespace Clarisa.Application.Features.Secciones;

public sealed class GestionSecciones(IFabricaUnidadTrabajo fabrica, IReloj reloj)
{
    private const string Entidad = nameof(Seccion);

    public async Task<RegistroVersionado> CrearAsync(Actor actor, CrearSeccion solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        Error.DesdeDominio(() => new Seccion(datos.Id, datos.AnioLectivoId, datos.Grado, datos.Nombre, version: 1));

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var grado = await GestionGrados.ExigirActivoAsync(unidad, datos.Grado, ct);
        var nuevo = Error.DesdeDominio(() => new Seccion(datos.Id, datos.AnioLectivoId, grado, datos.Nombre, version: 1));
        var existente = await unidad.Secciones.ObtenerAsync(nuevo.Id, ct);
        if (existente is not null) return Reintentos.Resolver(existente.MismoContenido(nuevo), existente.Id, existente.Version);
        if (await unidad.AniosLectivos.ObtenerAsync(nuevo.AnioLectivoId, ct) is null)
            throw Error.ReferenciaInvalida("El año lectivo seleccionado no existe.", nameof(Seccion.AnioLectivoId));
        await ExigirNombreLibreAsync(unidad, nuevo, null, ct);

        await unidad.Secciones.AgregarAsync(nuevo, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Creacion, Entidad, nuevo.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(nuevo.Id, nuevo.Version);
    }

    public async Task<RegistroVersionado> ModificarAsync(Actor actor, ModificarSeccion solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var seccion = await unidad.Secciones.ObtenerAsync(datos.Id, ct) ?? throw Error.NoEncontrado("La sección no existe.");
        Error.ExigirVersion(seccion.Version, datos.VersionEsperada);
        var gradoAnterior = seccion.Grado;
        var grado = Textos.Iguales(gradoAnterior, datos.Grado?.Trim()) ? gradoAnterior
            : await GestionGrados.ExigirActivoAsync(unidad, datos.Grado, ct);
        Error.DesdeDominio(() => seccion.Modificar(grado, datos.Nombre));
        if (!Textos.Iguales(gradoAnterior, seccion.Grado) && await unidad.Calificaciones.ExistenEnSeccionAsync(seccion.Id, ct))
            throw Error.EnUso("La sección ya tiene resultados o historial de calificaciones. No se cambia su grado; crea la sección correcta para nuevas inscripciones.");
        await ExigirNombreLibreAsync(unidad, seccion, seccion.Id, ct);

        var version = await unidad.Secciones.ActualizarAsync(seccion, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Modificacion, Entidad, seccion.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(seccion.Id, version);
    }

    public async Task EliminarAsync(Actor actor, EliminarRegistro solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var seccion = await unidad.Secciones.ObtenerAsync(datos.Id, ct) ?? throw Error.NoEncontrado("La sección no existe.");
        Error.ExigirVersion(seccion.Version, datos.VersionEsperada);
        if (await unidad.Inscripciones.ExistenEnSeccionAsync(seccion.Id, ct))
            throw Error.EnUso("La sección tiene inscripciones registradas y no se puede eliminar.");
        if (await unidad.AsignacionesDocentes.ExistenEnSeccionAsync(seccion.Id, ct))
            throw Error.EnUso("La sección tiene asignaciones docentes y no se puede eliminar.");

        await unidad.Secciones.EliminarAsync(seccion.Id, seccion.Version, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Eliminacion, Entidad, seccion.Id, ct);
        await unidad.ConfirmarAsync(ct);
    }

    public async Task<SeccionDatos> ObtenerAsync(Actor actor, Guid id, CancellationToken ct)
    {
        Autorizacion.ExigirPersonal(actor);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        var seccion = await unidad.Secciones.ObtenerAsync(id, ct) ?? throw Error.NoEncontrado("La sección no existe.");
        return SeccionDatos.Desde(seccion);
    }

    public async Task<Pagina<SeccionDatos>> ListarPorAnioAsync(Actor actor, Guid anioLectivoId, ConsultaPagina consulta,
        CancellationToken ct)
    {
        Autorizacion.ExigirPersonal(actor);
        var pagina = ConsultaPagina.Validar(consulta);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        if (await unidad.AniosLectivos.ObtenerAsync(anioLectivoId, ct) is null) throw Error.NoEncontrado("El año lectivo no existe.");
        var resultado = await unidad.Secciones.ListarPorAnioAsync(anioLectivoId, pagina, ct);
        return resultado.Convertir(SeccionDatos.Desde);
    }

    private static async Task ExigirNombreLibreAsync(IUnidadTrabajo unidad, Seccion seccion, Guid? excluirId, CancellationToken ct)
    {
        if (await unidad.Secciones.ExisteNombreAsync(seccion.AnioLectivoId, seccion.Grado, seccion.Nombre, excluirId, ct))
            throw Error.Duplicado("Ya existe una sección con ese grado y nombre en el año lectivo.", nameof(Seccion.Nombre));
    }
}
