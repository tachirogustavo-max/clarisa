using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Auditoria;
using Clarisa.Application.Common.Consultas;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Resultados;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Domain.Common;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Docentes;

public sealed class GestionDocentes(IFabricaUnidadTrabajo fabrica, IReloj reloj)
{
    private const string Entidad = nameof(Docente);

    public async Task<RegistroVersionado> CrearAsync(Actor actor, CrearDocente solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        var nuevo = Error.DesdeDominio(() =>
            new Docente(datos.Id, datos.Identificacion, datos.Nombres, datos.Apellidos, datos.Correo, activo: true, version: 1));

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var existente = await unidad.Docentes.ObtenerAsync(nuevo.Id, ct);
        if (existente is not null) return Reintentos.Resolver(existente.MismoContenido(nuevo), existente.Id, existente.Version);
        await ExigirIdentificacionLibreAsync(unidad, nuevo.Identificacion, null, ct);

        await unidad.Docentes.AgregarAsync(nuevo, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Creacion, Entidad, nuevo.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(nuevo.Id, nuevo.Version);
    }

    public async Task<RegistroVersionado> ModificarAsync(Actor actor, ModificarDocente solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var docente = await unidad.Docentes.ObtenerAsync(datos.Id, ct) ?? throw Error.NoEncontrado("El docente no existe.");
        Error.ExigirVersion(docente.Version, datos.VersionEsperada);
        var seguiraActivo = docente.Activo && datos.Activo;
        Error.DesdeDominio(() => docente.Modificar(datos.Identificacion, datos.Nombres, datos.Apellidos, datos.Correo, datos.Activo));
        await ExigirIdentificacionLibreAsync(unidad, docente.Identificacion, docente.Id, ct);

        var version = await unidad.Docentes.ActualizarAsync(docente, ct);
        // Mientras la ficha está inactiva no se emiten sesiones; toda sesión que exista al dar de baja o al reactivar es anterior a la
        // baja y no debe revivir. La comprobación de docente activo en cada petición queda como segunda barrera.
        if (!seguiraActivo) await unidad.AccesosDocentes.RevocarSesionesAsync(docente.Id, Instantes.Utc(reloj.Ahora), ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Modificacion, Entidad, docente.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(docente.Id, version);
    }

    public async Task<DocenteDatos> ObtenerAsync(Actor actor, Guid id, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        var docente = await unidad.Docentes.ObtenerAsync(id, ct) ?? throw Error.NoEncontrado("El docente no existe.");
        return DocenteDatos.Desde(docente);
    }

    public async Task<Pagina<DocenteDatos>> BuscarAsync(Actor actor, FiltroTexto filtro, ConsultaPagina consulta, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var filtroValido = FiltroTexto.Validar(filtro);
        var pagina = ConsultaPagina.Validar(consulta);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        var resultado = await unidad.Docentes.BuscarAsync(filtroValido, pagina, ct);
        return resultado.Convertir(DocenteDatos.Desde);
    }

    private static async Task ExigirIdentificacionLibreAsync(IUnidadTrabajo unidad, string identificacion, Guid? excluirId,
        CancellationToken ct)
    {
        if (await unidad.Docentes.ExisteIdentificacionAsync(identificacion, excluirId, ct))
            throw Error.Duplicado("Ya existe un docente con esa identificación.", nameof(Docente.Identificacion));
    }
}
