using MySqlConnector;

namespace Clarisa.Infrastructure.Persistence.Esquema;

internal sealed record Diagnostico(EstadoEsquema Estado, IReadOnlySet<int> Registradas, IReadOnlyList<string> Motivos);

// Solo lee el catálogo y la tabla de versiones: nunca modifica una base, sea propia o ajena.
internal static class DiagnosticoEsquema
{
    public static async Task<Diagnostico> DiagnosticarAsync(MySqlConnection conexion, int segundosComando, CancellationToken ct)
    {
        var objetos = await VerificadorEsquema.LeerAsync(conexion, segundosComando, ct,
            "SELECT TABLE_NAME, TABLE_COMMENT FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE()",
            fila => (Nombre: fila.GetString(0), Comentario: fila.GetString(1)));
        var rutinas = (await VerificadorEsquema.LeerAsync(conexion, segundosComando, ct, """
            SELECT (SELECT COUNT(*) FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA = DATABASE())
                 + (SELECT COUNT(*) FROM information_schema.EVENTS WHERE EVENT_SCHEMA = DATABASE())
            """, fila => Convert.ToInt64(fila.GetValue(0), System.Globalization.CultureInfo.InvariantCulture)))[0];

        if (objetos.Count == 0 && rutinas == 0) return Resultado(EstadoEsquema.Vacia);
        var versiones = objetos.FirstOrDefault(objeto => objeto.Nombre == EsquemaEsperado.TablaVersiones);
        if (versiones.Nombre is null)
            return Resultado(EstadoEsquema.Ajena, "La base tiene objetos y no tiene la tabla de versiones de esta aplicación.");
        if (versiones.Comentario != EsquemaEsperado.MarcaPropietario)
            return Resultado(EstadoEsquema.Ajena, "La tabla de versiones no tiene la marca de esta aplicación.");

        var registros = await VerificadorEsquema.LeerAsync(conexion, segundosComando, ct,
            $"SELECT version, nombre, huella FROM {EsquemaEsperado.TablaVersiones} ORDER BY version",
            fila => (Version: fila.GetInt32(0), Nombre: fila.GetString(1), Huella: fila.GetString(2)));
        var conocidas = CatalogoMigraciones.Disponibles.ToDictionary(migracion => migracion.Version);
        var registradas = registros.Select(registro => registro.Version).ToHashSet();

        if (registros.Any(registro => registro.Version > CatalogoMigraciones.VersionMaxima))
            return Resultado(EstadoEsquema.MasNueva, $"La base tiene la versión {registros.Max(r => r.Version)} y esta aplicación conoce hasta "
                + $"la {CatalogoMigraciones.VersionMaxima}.");
        var motivos = registros
            .Where(registro => !conocidas.TryGetValue(registro.Version, out var migracion)
                || migracion.Nombre != registro.Nombre || migracion.Huella != registro.Huella)
            .Select(registro => $"La migración registrada {registro.Version} ({registro.Nombre}) no coincide con la incluida en la aplicación.")
            .ToList();
        if (registradas.Count > 0 && registradas.Max() != registradas.Count)
            motivos.Add("Las versiones registradas no son consecutivas desde 1.");
        if (motivos.Count > 0) return new Diagnostico(EstadoEsquema.Alterada, registradas, motivos);

        return new Diagnostico(registradas.Count == CatalogoMigraciones.VersionMaxima ? EstadoEsquema.AlDia : EstadoEsquema.Pendiente,
            registradas, []);

        Diagnostico Resultado(EstadoEsquema estado, params string[] razones) => new(estado, new HashSet<int>(), razones);
    }
}
