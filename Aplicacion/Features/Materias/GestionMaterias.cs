using Clarisa.Application.Abstractions;
using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Auditoria;
using Clarisa.Application.Common.Consultas;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Resultados;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Materias;

public sealed class GestionMaterias(IFabricaUnidadTrabajo fabrica, IReloj reloj)
{
    private const string Entidad = nameof(Materia);

    public async Task<RegistroVersionado> CrearAsync(Actor actor, CrearMateria solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        var nuevo = Error.DesdeDominio(() => new Materia(datos.Id, datos.Codigo, datos.Nombre, activa: true, version: 1));

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var existente = await unidad.Materias.ObtenerAsync(nuevo.Id, ct);
        if (existente is not null) return Reintentos.Resolver(existente.MismoContenido(nuevo), existente.Id, existente.Version);
        await ExigirCodigoLibreAsync(unidad, nuevo.Codigo, null, ct);

        await unidad.Materias.AgregarAsync(nuevo, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Creacion, Entidad, nuevo.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(nuevo.Id, nuevo.Version);
    }

    public async Task<RegistroVersionado> ModificarAsync(Actor actor, ModificarMateria solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);

        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Escritura, ct);
        var materia = await unidad.Materias.ObtenerAsync(datos.Id, ct) ?? throw Error.NoEncontrado("La materia no existe.");
        Error.ExigirVersion(materia.Version, datos.VersionEsperada);
        Error.DesdeDominio(() => materia.Modificar(datos.Codigo, datos.Nombre, datos.Activa));
        await ExigirCodigoLibreAsync(unidad, materia.Codigo, materia.Id, ct);

        var version = await unidad.Materias.ActualizarAsync(materia, ct);
        await Auditar.RegistrarAsync(unidad, reloj, actor, AccionAuditoria.Modificacion, Entidad, materia.Id, ct);
        await unidad.ConfirmarAsync(ct);
        return new RegistroVersionado(materia.Id, version);
    }

    public async Task<MateriaDatos> ObtenerAsync(Actor actor, Guid id, CancellationToken ct)
    {
        Autorizacion.ExigirPersonal(actor);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        var materia = await unidad.Materias.ObtenerAsync(id, ct) ?? throw Error.NoEncontrado("La materia no existe.");
        return MateriaDatos.Desde(materia);
    }

    public async Task<Pagina<MateriaDatos>> BuscarAsync(Actor actor, FiltroTexto filtro, ConsultaPagina consulta, CancellationToken ct)
    {
        Autorizacion.ExigirPersonal(actor);
        var filtroValido = FiltroTexto.Validar(filtro);
        var pagina = ConsultaPagina.Validar(consulta);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        var resultado = await unidad.Materias.BuscarAsync(filtroValido, pagina, ct);
        return resultado.Convertir(MateriaDatos.Desde);
    }

    private static async Task ExigirCodigoLibreAsync(IUnidadTrabajo unidad, string codigo, Guid? excluirId, CancellationToken ct)
    {
        if (await unidad.Materias.ExisteCodigoAsync(codigo, excluirId, ct))
            throw Error.Duplicado("Ya existe una materia con ese código.", nameof(Materia.Codigo));
    }
}
