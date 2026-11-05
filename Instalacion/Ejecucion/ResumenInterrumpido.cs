using Clarisa.Instalacion.Contratos;

namespace Clarisa.Instalacion.Ejecucion;

internal static class ResumenInterrumpido
{
    public static ResultadoInstalacion Crear(PlanInstalacion plan, IReadOnlyList<IPasoInstalacion> pasos,
        IReadOnlyList<ResultadoPaso> registrados, IPasoInstalacion? actual, string archivoRegistro, string causa)
    {
        var resultados = registrados.ToDictionary(p => p.Id);
        var fallido = actual ?? pasos.FirstOrDefault();
        var detalle = "La instalación se detuvo al registrar o confirmar su avance: " + causa;
        if (fallido is not null)
            resultados[fallido.Id] = new(fallido.Id, fallido.Nombre, fallido.Critico, EstadoPaso.Fallido, detalle);
        var salida = pasos.Select(p => resultados.GetValueOrDefault(p.Id)
            ?? new ResultadoPaso(p.Id, p.Nombre, p.Critico, EstadoPaso.NoEjecutado,
                "No se ejecutó después de la interrupción del registro.")).ToArray();
        return new(plan.Id, plan.Modo, EstadoInstalacion.Fallida, salida, archivoRegistro,
            [detalle, "El archivo de estado o el registro pueden estar incompletos. No se garantiza un resumen guardado en disco. "
                + "Revisa el espacio y los permisos antes de reintentar; no borres datos ni claves para continuar."]);
    }
}
