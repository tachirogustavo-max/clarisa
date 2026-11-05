using Clarisa.Domain.Exceptions;

namespace Clarisa.Application.Common.Errores;

public static class Error
{
    public static ErrorAplicacionException SesionNoValida() =>
        new(TipoError.SesionNoValida, "Tu sesión terminó o sus permisos cambiaron. Inicia sesión nuevamente.");
    public static ErrorAplicacionException DatosInvalidos(string mensaje, string? campo = null) =>
        new(TipoError.DatosInvalidos, mensaje, campo);

    public static ErrorAplicacionException AccesoDenegado(string mensaje = "No tiene permiso para realizar esta operación.") =>
        new(TipoError.AccesoDenegado, mensaje);

    public static ErrorAplicacionException NoEncontrado(string mensaje) => new(TipoError.NoEncontrado, mensaje);

    public static ErrorAplicacionException Duplicado(string mensaje, string? campo = null) =>
        new(TipoError.Duplicado, mensaje, campo);

    public static ErrorAplicacionException ReferenciaInvalida(string mensaje, string? campo = null) =>
        new(TipoError.ReferenciaInvalida, mensaje, campo);

    public static ErrorAplicacionException EnUso(string mensaje) => new(TipoError.EnUso, mensaje);

    public static ErrorAplicacionException VersionObsoleta(string? campo = null) =>
        new(TipoError.VersionObsoleta, "Otro usuario modificó este registro. Recargue los datos antes de continuar.", campo);

    public static ErrorAplicacionException VentanaCerrada() =>
        new(TipoError.VentanaCerrada, "La carga de notas de este momento no está abierta. Solicite a la dirección que la habilite.");

    public static ErrorAplicacionException EstadoNoPermitido(string mensaje, string? campo = null) =>
        new(TipoError.EstadoNoPermitido, mensaje, campo);

    public static ErrorAplicacionException AlmacenNoDisponible(Exception causa) =>
        new(TipoError.AlmacenNoDisponible, "No se pudo acceder a los datos del servidor. La operación no se ha confirmado.", causa: causa);

    public static ErrorAplicacionException ResultadoDesconocido(Exception causa) =>
        new(TipoError.ResultadoDesconocido, "No se pudo confirmar el resultado. Consulta el estado antes de repetir la operación.", causa: causa);

    public static T SolicitudRequerida<T>(T? solicitud) where T : class =>
        solicitud ?? throw DatosInvalidos("La solicitud está vacía.");

    public static void ExigirVersion(long versionActual, long versionEsperada)
    {
        if (versionActual != versionEsperada) throw VersionObsoleta();
    }

    public static T DesdeDominio<T>(Func<T> operacion)
    {
        try
        {
            return operacion();
        }
        catch (ReglaDominioException error)
        {
            throw new ErrorAplicacionException(TipoError.DatosInvalidos, error.Message, error.Campo, error);
        }
    }

    public static void DesdeDominio(Action operacion) => DesdeDominio(() =>
    {
        operacion();
        return true;
    });
}
