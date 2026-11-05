using Clarisa.Application.Abstractions.Persistencia;
using Clarisa.Application.Common.Errores;
using Clarisa.Application.Features.Inscripciones;
using Clarisa.Domain.Common;
using Clarisa.Domain.Entities;

namespace Clarisa.Application.Features.Calificaciones;

internal static class LoteCalificaciones
{
    public const int MaximoNotas = 200;

    public static string Campo(int indice, string propiedad) => $"{nameof(GuardarCalificaciones.Notas)}[{indice}].{propiedad}";

    // Devuelve las notas con la letra normalizada; la pertenencia a la escala y al plan se comprueba dentro de la unidad.
    // Sin política vigente se conserva el límite técnico de dos decimales; nunca se inventa una precisión académica.
    public static int DecimalesCaptura(PoliticaAcademica? politica) =>
        politica?.Reglas.DecimalesCaptura ?? EscalaNotas.DecimalesTecnicosMaximos;

    public static IReadOnlyList<NotaEnLote> Validar(GuardarCalificaciones solicitud)
    {
        if (solicitud.Notas is not { Count: > 0 and <= MaximoNotas } recibidas)
            throw Error.DatosInvalidos($"Guarda entre 1 y {MaximoNotas} notas cada vez.", nameof(GuardarCalificaciones.Notas));

        var notas = new List<NotaEnLote>(recibidas.Count);
        var vistas = new HashSet<Guid>();
        for (var indice = 0; indice < recibidas.Count; indice++)
        {
            var nota = recibidas[indice] ?? throw Error.DatosInvalidos("Hay una fila vacía en el lote.", Campo(indice, nameof(NotaEnLote.InscripcionId)));
            if (nota.InscripcionId == Guid.Empty || !vistas.Add(nota.InscripcionId))
                throw Error.DatosInvalidos("Cada estudiante debe aparecer una sola vez en el lote.", Campo(indice, nameof(NotaEnLote.InscripcionId)));
            if (nota.VersionEsperada < 0)
                throw Error.DatosInvalidos("La versión esperada no es válida.", Campo(indice, nameof(NotaEnLote.VersionEsperada)));
            Error.DesdeDominio(() => EscalaNotas.ValidarOpcional(nota.Valor, EscalaNotas.DecimalesTecnicosMaximos, Campo(indice, nameof(NotaEnLote.Valor))));
            var letra = string.IsNullOrWhiteSpace(nota.Letra) ? null : NormalizarLetra(nota.Letra, Campo(indice, nameof(NotaEnLote.Letra)));
            if (nota.Inasistencias is < 0 or > Calificacion.MaximoInasistencias)
                throw Error.DatosInvalidos($"Las inasistencias deben estar entre 0 y {Calificacion.MaximoInasistencias}.",
                    Campo(indice, nameof(NotaEnLote.Inasistencias)));
            notas.Add(nota with { Letra = letra, Inasistencias = nota.IncluyeInasistencias ? nota.Inasistencias : null });
        }
        return notas;
    }

    public static void ExigirDecimales(IReadOnlyList<NotaEnLote> notas, int decimales)
    {
        for (var indice = 0; indice < notas.Count; indice++)
            Error.DesdeDominio(() => EscalaNotas.ValidarOpcional(notas[indice].Valor, decimales, Campo(indice, nameof(NotaEnLote.Valor))));
    }

    public static void ExigirModalidad(IReadOnlyList<NotaEnLote> notas, ReglasEvaluacion reglas)
    {
        if (!ReglasModalidad.AdmiteCarga(reglas.Modalidad))
            throw Error.EstadoNoPermitido("Esta materia no se cursa en el grado según el plan del año.", nameof(GuardarCalificaciones.MateriaId));
        for (var indice = 0; indice < notas.Count; indice++)
        {
            var nota = notas[indice];
            var tipo = Calificacion.TipoDe(nota.Valor, nota.Letra);
            if (ReglasModalidad.Admite(reglas.Modalidad, tipo))
            {
                if (nota.Letra is { } letra && !reglas.LetraValida(letra))
                    throw Error.DatosInvalidos("La letra no está en la escala configurada para este año.", Campo(indice, nameof(NotaEnLote.Letra)));
                continue;
            }
            var (mensaje, propiedad) = reglas.Modalidad switch
            {
                ModalidadEvaluacion.Cualitativa => ("Esta materia se evalúa solo con letra.", nameof(NotaEnLote.Valor)),
                ModalidadEvaluacion.NumericaYCualitativa => ("Esta materia requiere nota y letra juntas, o ninguna si está pendiente.",
                    nota.Letra is null ? nameof(NotaEnLote.Letra) : nameof(NotaEnLote.Valor)),
                _ => ("Esta materia no admite letras: escribe solo la nota numérica.", nameof(NotaEnLote.Letra))
            };
            throw Error.DatosInvalidos(mensaje, Campo(indice, propiedad));
        }
    }

    public static async Task ExigirInscripcionesAsync(IUnidadTrabajo unidad, Guid seccionId, IReadOnlyList<NotaEnLote> notas,
        CancellationToken ct)
    {
        var inscripciones = (await unidad.Inscripciones.ObtenerVariasAsync(notas.Select(nota => nota.InscripcionId).ToList(), ct))
            .ToDictionary(inscripcion => inscripcion.Id);
        var estudiantes = await Nominas.EstudiantesDeAsync(unidad, inscripciones.Values, ct);
        for (var indice = 0; indice < notas.Count; indice++)
            ExigirInscripcion(inscripciones.GetValueOrDefault(notas[indice].InscripcionId), estudiantes, seccionId,
                Campo(indice, nameof(NotaEnLote.InscripcionId)));
    }

    public static void ExigirInscripcion(Inscripcion? inscripcion, IReadOnlyDictionary<Guid, Estudiante> estudiantes, Guid seccionId,
        string campo)
    {
        if (inscripcion is null || inscripcion.SeccionId != seccionId)
            throw Error.ReferenciaInvalida("La inscripción no pertenece a esta sección.", campo);
        if (inscripcion.Estado != EstadoInscripcion.Activa)
            throw Error.EstadoNoPermitido("El estudiante fue retirado de la sección.", campo);
        if (!estudiantes.TryGetValue(inscripcion.EstudianteId, out var estudiante) || !estudiante.Activo)
            throw Error.EstadoNoPermitido("El estudiante no está activo.", campo);
    }

    // Una exoneración solo la cambia la dirección con su operación propia. Una fila vacía con la versión vigente es el eco de la
    // planilla que envía un cliente anterior a los estados, y no cambia nada.
    public static bool EsEcoDeExoneracion(NotaEnLote nota, Calificacion? actual, int indice)
    {
        if (actual is not { Exonerada: true }) return false;
        if (nota.Valor is null && nota.Letra is null && nota.Inasistencias is null && nota.VersionEsperada == actual.Version) return true;
        throw Error.EstadoNoPermitido("El estudiante está exonerado en esta materia y momento; solo la dirección puede revocarlo.",
            Campo(indice, nameof(NotaEnLote.Valor)));
    }

    // Un resultado igual al guardado no es conflicto: así un doble envío o un reintento tras perder la respuesta no falla.
    public static void ExigirVersiones(IReadOnlyList<NotaEnLote> notas, IReadOnlyDictionary<Guid, Calificacion> guardadas)
    {
        for (var indice = 0; indice < notas.Count; indice++)
        {
            var nota = notas[indice];
            var actual = guardadas.GetValueOrDefault(nota.InscripcionId);
            if (EsEcoDeExoneracion(nota, actual, indice) || SinCambio(nota, actual)) continue;
            if ((actual?.Version ?? 0) != nota.VersionEsperada)
                throw Error.VersionObsoleta(Campo(indice, nameof(NotaEnLote.VersionEsperada)));
        }
    }

    public static bool SinCambio(NotaEnLote nota, Calificacion? actual) =>
        actual?.MismoResultado(nota.Valor, nota.Letra, Inasistencias(nota, actual))
        ?? (nota.Valor is null && nota.Letra is null && nota.Inasistencias is null);

    // Sin inasistencias en el envío se conservan las guardadas.
    public static int? Inasistencias(NotaEnLote nota, Calificacion? actual) =>
        nota.IncluyeInasistencias ? nota.Inasistencias : actual?.Inasistencias;

    private static string NormalizarLetra(string letra, string campo)
    {
        try
        {
            return LetraCualitativa.Normalizar(letra);
        }
        catch (Domain.Exceptions.ReglaDominioException error)
        {
            throw Error.DatosInvalidos(error.Message, campo);
        }
    }
}
