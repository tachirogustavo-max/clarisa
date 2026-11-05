namespace Clarisa.Instalacion.Registro;

// Toda cadena que llega al registro o al resultado pasa por aquí. Los pasos registran cada secreto en cuanto lo generan o reciben.
internal sealed class FiltroSecretos
{
    private const string Oculto = "«oculto»";
    private const int MaximoDetalle = 600;
    private readonly List<string> secretos = [];
    private readonly Lock cerrojo = new();

    public void Registrar(string? secreto)
    {
        if (string.IsNullOrEmpty(secreto) || secreto.Length < 4) return;
        lock (cerrojo)
        {
            if (!secretos.Contains(secreto, StringComparer.Ordinal)) secretos.Add(secreto);
        }
    }

    public string Limpiar(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return "";
        var limpio = texto;
        lock (cerrojo)
        {
            foreach (var secreto in secretos.OrderByDescending(s => s.Length))
                limpio = limpio.Replace(secreto, Oculto, StringComparison.Ordinal);
        }
        limpio = new string(limpio.Select(c => char.IsControl(c) && c is not '\n' ? ' ' : c).ToArray());
        return limpio.Length <= MaximoDetalle ? limpio : limpio[..MaximoDetalle] + "…";
    }
}
