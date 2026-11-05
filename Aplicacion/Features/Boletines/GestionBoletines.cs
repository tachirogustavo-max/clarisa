using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Common.Seguridad;
using Clarisa.Application.Features.Inscripciones;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Boletines;

// Con un cierre vigente el boletín sale de su copia fija y de la política que se aplicó; sin cierre, de los datos actuales y la
// política vigente, y se marca provisional (Cierre nulo).
public sealed class GestionBoletines(IFabricaUnidadTrabajo fabrica)
{
    private sealed record Fuente(DatosSeccion Datos, PoliticaAcademica? Politica, CierreResultados? Cierre,
        IReadOnlyDictionary<Guid, Materia> Materias, ILookup<Guid, ResultadoCerrado> Resultados,
        IReadOnlyDictionary<(Guid Inscripcion, Guid Materia, Guid Lapso), int> Faltas);

    public async Task<BoletinDatos> BoletinAsync(Actor actor, Guid inscripcionId, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        var inscripcion = await unidad.Inscripciones.ObtenerAsync(inscripcionId, ct) ?? throw Error.NoEncontrado("La inscripción no existe.");
        var estudiante = await unidad.Estudiantes.ObtenerAsync(inscripcion.EstudianteId, ct)
            ?? throw Error.ReferenciaInvalida("La inscripción apunta a un estudiante inexistente.");
        var datos = await LecturaResultados.LeerSeccionAsync(unidad, inscripcion.SeccionId, ct);
        var fuente = await LeerFuenteAsync(unidad, datos, [inscripcion.Id], ct);
        return Armar(new Inscrito(inscripcion, estudiante), fuente);
    }

    public async Task<BoletinDatos> BoletinAsync(Actor actor, ConsultarBoletin solicitud, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        var datos = Error.SolicitudRequerida(solicitud);
        if (datos.AnioLectivoId == Guid.Empty || datos.SeccionId == Guid.Empty || datos.EstudianteId == Guid.Empty || datos.LapsoId == Guid.Empty)
            throw Error.DatosInvalidos("Selecciona las referencias del boletín.");
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        var seccion = await LecturaResultados.LeerSeccionAsync(unidad, datos.SeccionId, ct);
        if (seccion.Anio.Id != datos.AnioLectivoId) throw Error.DatosInvalidos("La sección no pertenece al año seleccionado.", "SeccionId");
        if (datos.LapsoId is { } lapso && seccion.Lapsos.All(l => l.Id != lapso))
            throw Error.DatosInvalidos("El momento no pertenece al año seleccionado.", "LapsoId");
        var inscrito = (await Nominas.ActivosAsync(unidad, datos.SeccionId, ct)).SingleOrDefault(i => i.Estudiante.Id == datos.EstudianteId)
            ?? throw Error.NoEncontrado("El estudiante no tiene una inscripción activa en esta sección.");
        var boletin = Armar(inscrito, await LeerFuenteAsync(unidad, seccion, [inscrito.Inscripcion.Id], ct));
        return datos.LapsoId is not { } elegido ? boletin : SoloLapso(boletin, elegido);
    }

    public async Task<IReadOnlyList<BoletinDatos>> BoletinesSeccionAsync(Actor actor, Guid seccionId, CancellationToken ct)
    {
        Autorizacion.ExigirAdministracion(actor);
        await using var unidad = await fabrica.IniciarAsync(ModoUnidadTrabajo.Lectura, ct);
        var datos = await LecturaResultados.LeerSeccionAsync(unidad, seccionId, ct);
        var inscritos = await Nominas.ActivosAsync(unidad, seccionId, ct);
        var fuente = await LeerFuenteAsync(unidad, datos, inscritos.Select(i => i.Inscripcion.Id).ToList(), ct);
        return inscritos.Select(inscrito => Armar(inscrito, fuente)).ToList();
    }

    public static BoletinDatos SoloLapso(BoletinDatos boletin, Guid lapsoId) => boletin with
    {
        Lapsos = boletin.Lapsos.Where(l => l.LapsoId == lapsoId).ToArray(),
        Promedios = boletin.Promedios.Where(p => p.LapsoId == lapsoId).ToArray(),
        Materias = boletin.Materias.Select(m => m with { Notas = m.Notas.Where(n => n.LapsoId == lapsoId).ToArray() }).ToArray()
    };

    private static async Task<Fuente> LeerFuenteAsync(IUnidadTrabajo unidad, DatosSeccion datos, IReadOnlyList<Guid> inscripciones,
        CancellationToken ct)
    {
        var cierre = await unidad.Cierres.VigenteAsync(datos.Seccion.Id, ct);
        // Las inasistencias se leen siempre de las notas: con un cierre vigente nadie puede cambiarlas.
        var calificaciones = await unidad.Calificaciones.ListarPorInscripcionesAsync(inscripciones, ct);
        var faltas = calificaciones.Where(c => c.Inasistencias is not null)
            .ToDictionary(c => (c.InscripcionId, c.MateriaId, c.LapsoId), c => c.Inasistencias!.Value);
        IReadOnlyList<ResultadoCerrado> resultados;
        PoliticaAcademica? politica;
        if (cierre is not null)
        {
            politica = await unidad.Politicas.ObtenerAsync(cierre.PoliticaId, ct)
                ?? throw Error.ReferenciaInvalida("El cierre apunta a una política inexistente.");
            resultados = await unidad.Cierres.ResultadosAsync(cierre.Id, inscripciones, ct);
        }
        else
        {
            politica = await unidad.Politicas.VigenteAsync(datos.Anio.Id, ct);
            resultados = inscripciones.SelectMany(id => LecturaResultados.Actuales(datos, id, calificaciones, politica)).ToList();
        }
        var materias = await LecturaResultados.LeerMateriasAsync(unidad, resultados.Select(r => r.MateriaId).Concat(datos.MateriasPrevistas), ct);
        return new Fuente(datos, politica, cierre, materias, resultados.ToLookup(r => r.InscripcionId), faltas);
    }

    private static BoletinDatos Armar(Inscrito inscrito, Fuente fuente)
    {
        var (datos, politica) = (fuente.Datos, fuente.Politica);
        var lapsoPorOrden = datos.Lapsos.ToDictionary(l => l.Orden, l => l.Id);
        var resultados = fuente.Resultados[inscrito.Inscripcion.Id].ToList();
        var filas = resultados
            .Select(r => (Resultado: r, Materia: fuente.Materias[r.MateriaId]))
            .OrderBy(par => par.Materia.Codigo, StringComparer.Ordinal)
            .Select(par => new MateriaBoletin(par.Materia.Id, par.Materia.Codigo, par.Materia.Nombre, par.Resultado.Lapsos
                .Where(l => lapsoPorOrden.ContainsKey(l.Orden))
                .Select(l => Celda(lapsoPorOrden[l.Orden], l, par.Resultado.Modalidad, politica) with
                {
                    Inasistencias = fuente.Faltas.TryGetValue((inscrito.Inscripcion.Id, par.Materia.Id, lapsoPorOrden[l.Orden]), out var n) ? n : null
                }).ToList())
            {
                Modalidad = par.Resultado.Modalidad,
                Definitiva = new DefinitivaBoletin(par.Resultado.Definitiva.Estado, par.Resultado.Definitiva.Valor,
                    FormatoResultado.Definitiva(par.Resultado.Definitiva.Estado, par.Resultado.Definitiva.Valor, politica?.Reglas.DecimalesResultado,
                        abreviado: true))
            })
            .ToList();
        var (inscripcion, estudiante) = (inscrito.Inscripcion, inscrito.Estudiante);
        return new BoletinDatos(inscripcion.Id, estudiante.Id, estudiante.Identificacion, estudiante.Nombres, estudiante.Apellidos,
            datos.Seccion.Id, datos.Seccion.Grado, datos.Seccion.Nombre, datos.Anio.Id, datos.Anio.Nombre, inscripcion.Estado,
            datos.Lapsos.Select(lapso => new LapsoBoletin(lapso.Id, lapso.Orden, lapso.Nombre)).ToList(), filas, PoliticaBoletin.Desde(politica))
        {
            Cierre = fuente.Cierre is { } cierre ? new CierreAplicadoBoletin(cierre.Id, cierre.Numero, cierre.Traza.CerradoEn) : null,
            Promedios = PromediosBoletin.Calcular(datos.Lapsos, resultados, filas, politica, fuente.Cierre is not null)
        };
    }

    private static NotaLapsoBoletin Celda(Guid lapsoId, LapsoCerrado lapso, ModalidadEvaluacion? modalidad, PoliticaAcademica? politica)
    {
        var estado = LecturaResultados.Estado(modalidad, lapso.Tipo);
        return new NotaLapsoBoletin(lapsoId, lapso.Orden, lapso.Valor)
        {
            Estado = estado,
            Letra = lapso.Letra,
            Texto = FormatoResultado.Texto(estado, lapso.Valor, lapso.Letra, politica?.Reglas.DecimalesCaptura, abreviado: true)
        };
    }
}
