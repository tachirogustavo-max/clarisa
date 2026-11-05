namespace Clarisa.Infrastructure.Respaldos.Formato;

internal enum TipoColumna
{
    Id = 1,
    Texto = 2,
    Fecha = 3,
    Entero = 4,
    Decimal = 5
}

internal sealed record ColumnaRespaldo(string Nombre, TipoColumna Tipo, bool Nula = false);

// Diferida: columna que se inserta como NULL y se completa después de cargar la tabla a la que apunta (ciclo de claves foráneas).
// Sembrada: la migración la llena con valores iniciales; al recuperar se vacía primero y quedan solo las filas de la copia.
internal sealed record TablaRespaldo(string Nombre, IReadOnlyList<ColumnaRespaldo> Columnas, string? Diferida = null, string? DespuesDe = null,
    bool Sembrada = false)
{
    public string Entrada => $"tablas/{Nombre}.jsonl";
    public string ClaveOrden => Columnas[0].Nombre;
}

// Lista cerrada en orden de inserción. Una migración que añada tablas o columnas obliga a actualizarla: la copia compara este catálogo
// con information_schema y se niega a continuar si algo no coincide.
internal static class CatalogoRespaldo
{
    public const int Formato = 1;

    public static readonly IReadOnlySet<string> Excluidas = new HashSet<string>(StringComparer.Ordinal)
    {
        "sesiones_usuario", "esquema_versiones", "bloqueo_escritura"
    };

    public static readonly IReadOnlyDictionary<string, string> Generadas = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["inscripciones"] = "activa_marca",
        ["politicas_academicas"] = "estado_unico",
        ["cierres_resultados"] = "vigente_marca"
    };

    public static readonly IReadOnlyList<TablaRespaldo> Tablas =
    [
        new("anios_lectivos", [Id("id"), Texto("nombre"), Texto("nombre_clave"), Fecha("inicio"), Fecha("fin"), Id("lapso_actual_id", true),
            Entero("version")], Diferida: "lapso_actual_id", DespuesDe: "lapsos"),
        new("lapsos", [Id("id"), Id("anio_lectivo_id"), Texto("nombre"), Texto("nombre_clave"), Entero("orden"), Fecha("inicio"),
            Fecha("fin"), Entero("version")]),
        new("estudiantes", [Id("id"), Texto("identificacion", true), Texto("nombres"), Texto("apellidos"), Fecha("fecha_nacimiento", true),
            Entero("activo"), Entero("version")]),
        new("docentes", [Id("id"), Texto("identificacion"), Texto("nombres"), Texto("apellidos"), Texto("correo", true), Entero("activo"),
            Entero("version")]),
        new("materias", [Id("id"), Texto("codigo"), Texto("nombre"), Entero("activa"), Entero("version")]),
        new("escala_cualitativa", [Id("id"), Id("anio_lectivo_id"), Texto("letra"), Texto("descripcion", true), Entero("version")]),
        new("grados", [Id("id"), Texto("nombre"), Texto("nombre_clave"), Entero("orden"), Entero("activo"), Entero("version")], Sembrada: true),
        new("plan_materias", [Id("id"), Id("anio_lectivo_id"), Texto("grado"), Texto("grado_clave"), Id("materia_id"), Entero("modalidad"),
            Entero("version")]),
        new("secciones", [Id("id"), Id("anio_lectivo_id"), Texto("grado"), Texto("grado_clave"), Texto("nombre"), Texto("nombre_clave"),
            Entero("version")]),
        new("inscripciones", [Id("id"), Id("estudiante_id"), Id("seccion_id"), Id("anio_lectivo_id"), Fecha("fecha"), Entero("estado"),
            Fecha("fecha_retiro", true), Entero("version")]),
        new("asignaciones_docentes", [Id("id"), Id("seccion_id"), Id("materia_id"), Id("docente_id"), Entero("version")]),
        new("ventanas_carga", [Id("id"), Id("lapso_id"), Entero("estado"), Entero("abierta_desde_utc_ticks"), Entero("hasta_utc_ticks", true),
            Entero("cerrada_en_utc_ticks", true), Entero("version")]),
        new("calificaciones", [Id("id"), Id("inscripcion_id"), Id("materia_id"), Id("lapso_id"), Id("anio_lectivo_id"), Decimal("valor", true),
            Entero("tipo"), Texto("letra", true), Texto("motivo_exoneracion", true), Entero("version"), Entero("inasistencias", true)]),
        new("politicas_academicas", [Id("id"), Id("anio_lectivo_id"), Entero("numero"), Entero("estado"), Entero("decimales_captura", true),
            Entero("decimales_resultado", true), Decimal("nota_aprobatoria", true), Entero("formula", true), Entero("redondeo", true),
            Entero("tratamiento_exonerado", true), Decimal("peso_lapso1", true), Decimal("peso_lapso2", true), Decimal("peso_lapso3", true),
            Texto("motivo"), Id("creada_por"), Entero("creada_utc_ticks"), Id("activada_por", true), Entero("activada_utc_ticks", true),
            Texto("motivo_activacion", true), Entero("version")]),
        new("cierres_resultados", [Id("id"), Id("anio_lectivo_id"), Id("seccion_id"), Entero("numero"), Id("politica_id"), Entero("estado"),
            Id("cerrado_por"), Entero("cerrado_utc_ticks"), Id("anulado_por", true), Entero("anulado_utc_ticks", true),
            Texto("motivo_anulacion", true), Entero("version")]),
        new("resultados_cerrados", [Entero("id"), Id("cierre_id"), Id("inscripcion_id"), Id("materia_id"), Entero("modalidad", true),
            Entero("tipo_lapso1"), Decimal("valor_lapso1", true), Texto("letra_lapso1", true), Entero("version_lapso1"),
            Entero("tipo_lapso2"), Decimal("valor_lapso2", true), Texto("letra_lapso2", true), Entero("version_lapso2"),
            Entero("tipo_lapso3"), Decimal("valor_lapso3", true), Texto("letra_lapso3", true), Entero("version_lapso3"),
            Entero("estado_definitiva"), Decimal("valor_definitiva", true)]),
        new("firmantes_boletin", [Id("id"), Entero("numero"), Texto("director", true), Texto("coordinador_evaluacion", true),
            Texto("coordinador_estudios", true), Texto("motivo"), Id("registrado_por"), Entero("registrado_utc_ticks")]),
        new("auditoria", [Entero("id"), Id("actor_usuario_id"), Entero("accion"), Texto("entidad"), Id("entidad_id"),
            Entero("ocurrido_utc_ticks")]),
        new("movimientos_ventana_carga", [Entero("id"), Id("ventana_id"), Id("lapso_id"), Entero("accion"), Entero("hasta_utc_ticks", true),
            Entero("version_resultante"), Id("actor_usuario_id"), Entero("ocurrido_utc_ticks")]),
        new("cambios_calificacion", [Entero("id"), Id("calificacion_id"), Id("inscripcion_id"), Id("materia_id"), Id("lapso_id"),
            Decimal("anterior", true), Decimal("nuevo", true), Entero("tipo_anterior", true), Entero("tipo_nuevo", true),
            Texto("letra_anterior", true), Texto("letra_nueva", true), Texto("motivo", true), Entero("version_resultante"),
            Id("actor_usuario_id"), Entero("ocurrido_utc_ticks"), Entero("inasistencias_anteriores", true), Entero("inasistencias_nuevas", true)]),
        new("cuentas_usuario", [Id("id"), Texto("usuario"), Texto("nombre"), Entero("perfil"), Id("docente_id", true), Texto("huella_clave"),
            Entero("activo"), Entero("version")]),
        new("auditoria_cuentas", [Entero("id"), Id("actor_usuario_id"), Entero("accion"), Id("cuenta_id"), Entero("ocurrido_utc_ticks")]),
        new("recuperaciones_cuenta", [Id("id"), Id("cuenta_id"), Entero("version_resultante"), Texto("identidad_windows"),
            Texto("usuario_base"), Texto("motivo"), Entero("ocurrido_utc_ticks")])
    ];

    public static TablaRespaldo? Buscar(string nombre) => Tablas.FirstOrDefault(t => t.Nombre == nombre);

    private static ColumnaRespaldo Id(string nombre, bool nula = false) => new(nombre, TipoColumna.Id, nula);
    private static ColumnaRespaldo Texto(string nombre, bool nula = false) => new(nombre, TipoColumna.Texto, nula);
    private static ColumnaRespaldo Fecha(string nombre, bool nula = false) => new(nombre, TipoColumna.Fecha, nula);
    private static ColumnaRespaldo Entero(string nombre, bool nula = false) => new(nombre, TipoColumna.Entero, nula);
    private static ColumnaRespaldo Decimal(string nombre, bool nula = false) => new(nombre, TipoColumna.Decimal, nula);
}
