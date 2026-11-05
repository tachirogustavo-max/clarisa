namespace Clarisa.Instalacion.Contratos;

// ClaveRecuperacion solo hace falta si la copia viene de otro servidor: es el archivo de recuperación de la clave de ese servidor.
public sealed record SolicitudRecuperacion(string Copia, string? ClaveRecuperacion = null);

// Resultado de comprobar la copia sin cambiar nada: Resumen son las líneas que describe la herramienta de mantenimiento.
public sealed record RevisionRecuperacion(bool Valida, string Mensaje, IReadOnlyList<string> Resumen);

public sealed record ResultadoRecuperacion(bool Completada, string Mensaje, IReadOnlyList<string> Pasos, string? Registro);

// Cuenta de MariaDB que usa la herramienta de mantenimiento; su contraseña es la que se eligió al instalar el servidor.
public static class CuentaMantenimiento
{
    public const string Usuario = "liceo_operador";
}
