using Clarisa.Application.Common.Errores;

namespace Clarisa.Infrastructure.Respaldos;

// Mensajes fijos para el operador y la dirección: sin rutas, SQL, claves ni contenido de filas. La causa técnica queda en InnerException.
internal static class ErroresRespaldo
{
    public static ErrorAplicacionException CopiaDanada(string detalle, Exception? causa = null) =>
        new(TipoError.DatosInvalidos, "La copia está dañada o incompleta: " + detalle, "Archivo", causa);

    public static ErrorAplicacionException ClaveNoCorresponde(Exception? causa = null) =>
        new(TipoError.DatosInvalidos, "La copia fue alterada o la clave no corresponde. No se recuperó ningún dato.", "Archivo", causa);

    public static ErrorAplicacionException OtraClave(Guid esperada) =>
        new(TipoError.DatosInvalidos, $"La copia se cifró con otra clave (Id {esperada}). Usa el archivo de recuperación de esa clave.",
            "Clave");

    public static ErrorAplicacionException SinClave() =>
        new(TipoError.EstadoNoPermitido, "El servidor no tiene preparada la clave de respaldos. Ejecuta la preparación en el servidor.");

    public static ErrorAplicacionException ClaveLocalIlegible(Exception? causa = null) =>
        new(TipoError.EstadoNoPermitido, "La clave protegida de este servidor no se puede leer. Usa el archivo de recuperación.",
            causa: causa);

    public static ErrorAplicacionException Excede(string limite) =>
        new(TipoError.EstadoNoPermitido, $"La copia supera el límite configurado de {limite}. No se procesó.");

    public static ErrorAplicacionException EsquemaNoReconocido(string detalle) =>
        new(TipoError.EstadoNoPermitido, "La estructura de la base no coincide con la que conoce el respaldo: " + detalle);

    public static ErrorAplicacionException Incompatible() =>
        new(TipoError.EstadoNoPermitido,
            "La copia pertenece a otra versión del esquema de datos. Esta versión de la aplicación no la recupera.");

    public static ErrorAplicacionException Almacen(Exception causa) => Error.AlmacenNoDisponible(causa);
}
