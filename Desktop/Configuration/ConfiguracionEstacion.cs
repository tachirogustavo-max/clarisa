using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Configuration;

public sealed record ConfiguracionEstacion(string Servidor, string? Aviso)
{
    public static ConfiguracionEstacion Leer(string[] argumentos)
    {
        var posiciones = argumentos.Select((valor, indice) => (valor, indice))
            .Where(x => x.valor.Equals("--servidor", StringComparison.OrdinalIgnoreCase) || x.valor.StartsWith("--servidor=", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (posiciones.Length == 0) return new(DireccionServidor.Predeterminada, null);
        const string aviso = "La dirección del servidor del acceso directo no es válida. Revísala en Conexión al servidor.";
        if (posiciones.Length != 1) return new("", aviso);
        var posicion = posiciones[0];
        var conIgual = posicion.valor.IndexOf('=');
        if (conIgual < 0 && posicion.indice + 1 >= argumentos.Length) return new("", aviso);
        var texto = conIgual >= 0 ? posicion.valor[(conIgual + 1)..] : argumentos[posicion.indice + 1];
        if (texto.Length > 250) return new("", aviso);
        var destino = DireccionServidor.Validar(texto);
        return destino.Direccion is { } direccion ? new(direccion.AbsoluteUri, null) : new("", aviso);
    }
}
