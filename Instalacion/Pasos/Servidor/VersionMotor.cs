using System.Diagnostics;
using Clarisa.Instalacion.Ejecucion;
using Clarisa.Instalacion.Registro;

namespace Clarisa.Instalacion.Pasos.Servidor;

// motor-servicio arranca el mariadbd del paquete sobre los datos existentes antes de migrar el esquema. Con otra versión del motor
// ese arranque puede cambiar archivos internos y la vuelta a los programas anteriores no está comprobada: se detiene antes de tocar nada.
// Solo se llama con una instalación anterior; una versión que no se puede leer cuenta como desconocida, nunca como igual.
internal static class VersionMotor
{
    private const string Ruta = "motor/bin/mariadbd.exe";
    private const string SinCambios = " No se detuvo la API ni se cambió nada.";

    public static string? Cambio(ContextoEjecucion contexto, PuntoActualizacion? pendiente)
    {
        var paquete = contexto.Paquete.Del(contexto.Plan.Modo).FirstOrDefault(a => a.Ruta == Ruta);
        if (paquete is null) return "El paquete no incluye el motor de datos " + Ruta + "." + SinCambios;
        var nueva = Leer(RutasSeguras.Dentro(contexto.Plan.Solicitud.OrigenPaquete, paquete.Ruta));
        if (nueva is null)
            return "No se pudo leer la versión del motor de datos del paquete; sin ella no se puede comprobar que sea la misma que usa la " +
                   "base existente." + SinCambios;
        // Tras un corte durante la copia de programas, el instalado puede ser ya el nuevo; el conservado en el punto es el anterior.
        var conservado = pendiente is { MigracionIniciada: false };
        var ruta = conservado
            ? RutasSeguras.Dentro(pendiente!.Directorio(contexto.RutasServidor), "programa/" + Ruta)
            : contexto.RutasServidor.EjecutableMotor;
        var instalada = Leer(ruta);
        if (instalada is null)
            return $"No se pudo leer la versión del motor de datos {(conservado ? "conservado en la actualización pendiente" : "instalado")} " +
                   $"({ruta}). Con datos existentes no se instala un motor de versión desconocida; revisa la instalación con el responsable " +
                   "técnico antes de continuar." + SinCambios;
        return nueva == instalada
            ? null
            : $"El paquete trae otra versión del motor de datos (instalada {instalada}, nueva {nueva}). Este instalador no actualiza el motor " +
              "sobre datos existentes porque su vuelta atrás no está comprobada." + SinCambios;
    }

    // Lee el recurso de versión de Windows del ejecutable; un archivo ausente, sin recurso o con versión vacía devuelve null.
    internal static string? Leer(string ruta)
    {
        if (!File.Exists(ruta)) return null;
        try
        {
            return FileVersionInfo.GetVersionInfo(ruta).FileVersion is { Length: > 0 } version ? version.Trim() : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
