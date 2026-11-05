using System.Data.SqlTypes;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Clarisa.Application.Common.Errores;
using Clarisa.Domain.Exceptions;
using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Sesion;

// Los mensajes son fijos: el texto del motor (con valores, SQL o nombres internos) solo queda en InnerException para registros.
internal static partial class TraduccionErrores
{
    private const int ClaveDuplicada = 1062;
    private const int ReferenciaHijaInexistente = 1452;
    private const int ReferenciaConDependientes = 1451;
    private const int RestriccionCheck = 4025;
    private const int SenalDisparador = 1644;

    private static readonly Dictionary<string, string> CamposUnicos = new(StringComparer.Ordinal)
    {
        ["PRIMARY"] = "Id",
        ["uq_anio_nombre"] = "Nombre",
        ["uq_lapso_orden"] = "Orden",
        ["uq_lapso_nombre"] = "Nombre",
        ["uq_estudiante_identificacion"] = "Identificacion",
        ["uq_docente_identificacion"] = "Identificacion",
        ["uq_materia_codigo"] = "Codigo",
        ["uq_seccion_grado_nombre"] = "Nombre",
        ["uq_grado_nombre"] = "Nombre",
        ["uq_inscripcion_activa"] = "EstudianteId",
        ["uq_asignacion_triple"] = "DocenteId",
        ["uq_ventana_lapso"] = "LapsoId",
        ["uq_calificacion_triple"] = "InscripcionId",
        ["uq_cuenta_usuario"] = "Usuario",
        ["uq_cuenta_docente"] = "DocenteId",
        ["uq_sesion_huella"] = "Token"
    };

    // Un historial con la misma versión resultante solo aparece si dos escritores modificaron el mismo registro.
    private static readonly HashSet<string> UnicosDeVersion = new(StringComparer.Ordinal)
    {
        "uq_movimiento_version", "uq_cambio_calificacion_version"
    };

    private static readonly Dictionary<string, string> CamposReferencias = new(StringComparer.Ordinal)
    {
        ["fk_lapso_anio"] = "AnioLectivoId",
        ["fk_anio_lapso_actual"] = "LapsoId",
        ["fk_seccion_anio"] = "AnioLectivoId",
        ["fk_inscripcion_estudiante"] = "EstudianteId",
        ["fk_inscripcion_seccion_anio"] = "SeccionId",
        ["fk_asignacion_seccion"] = "SeccionId",
        ["fk_asignacion_materia"] = "MateriaId",
        ["fk_asignacion_docente"] = "DocenteId",
        ["fk_ventana_lapso"] = "LapsoId",
        ["fk_calificacion_inscripcion_anio"] = "InscripcionId",
        ["fk_calificacion_lapso_anio"] = "LapsoId",
        ["fk_calificacion_materia"] = "MateriaId",
        ["fk_movimiento_ventana_lapso"] = "VentanaId",
        ["fk_cambio_calificacion_contexto"] = "CalificacionId",
        ["fk_cuenta_docente"] = "DocenteId",
        ["fk_sesion_cuenta"] = "CuentaId",
        ["fk_auditoria_cuenta"] = "CuentaId"
    };

    public static ErrorAplicacionException Traducir(MySqlException error) => error.Number switch
    {
        ClaveDuplicada when Restriccion(ClaveUnica(), error.Message) is { } nombre && UnicosDeVersion.Contains(nombre) =>
            new ErrorAplicacionException(TipoError.VersionObsoleta,
                "Otro usuario modificó este registro. Recargue los datos antes de continuar.", causa: error),
        ClaveDuplicada => new ErrorAplicacionException(TipoError.Duplicado, "Ya existe un registro con esos datos.",
            Campo(CamposUnicos, Restriccion(ClaveUnica(), error.Message)), error),
        ReferenciaHijaInexistente => new ErrorAplicacionException(TipoError.ReferenciaInvalida,
            "Un dato seleccionado no existe o no corresponde al mismo año lectivo.",
            Campo(CamposReferencias, Restriccion(ClaveForanea(), error.Message)), error),
        ReferenciaConDependientes => new ErrorAplicacionException(TipoError.EnUso,
            "El registro tiene información asociada y no se puede eliminar.", causa: error),
        RestriccionCheck => new ErrorAplicacionException(TipoError.DatosInvalidos,
            "Los datos no cumplen las reglas de almacenamiento.", causa: error),
        SenalDisparador => new ErrorAplicacionException(TipoError.EstadoNoPermitido,
            "El historial y la auditoría no se modifican ni se eliminan.", causa: error),
        _ => Error.AlmacenNoDisponible(error)
    };

    public static bool EsFalloTecnico(Exception error) =>
        error is IOException or SocketException or TimeoutException or InvalidOperationException or ObjectDisposedException;

    public static bool EsDatoDanado(Exception error) =>
        error is ReglaDominioException or DatoAlmacenadoInvalidoException or InvalidCastException or FormatException
            or SqlNullValueException or OverflowException or ArgumentException;

    private static string? Campo(Dictionary<string, string> campos, string? restriccion) =>
        restriccion is not null && campos.TryGetValue(restriccion, out var campo) ? campo : null;

    private static string? Restriccion(Regex patron, string mensaje)
    {
        var coincidencia = patron.Match(mensaje);
        return coincidencia.Success ? coincidencia.Groups["nombre"].Value : null;
    }

    [GeneratedRegex(@"for key '(?:[^'.]+\.)?(?<nombre>[A-Za-z0-9_]+)'")]
    private static partial Regex ClaveUnica();

    [GeneratedRegex(@"CONSTRAINT `(?<nombre>[A-Za-z0-9_]+)` FOREIGN KEY")]
    private static partial Regex ClaveForanea();
}
