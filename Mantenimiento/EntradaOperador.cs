using System.Text;
using Clarisa.Application.Features.Respaldos;

namespace Clarisa.Mantenimiento;

// La credencial de creación de bases se escribe en la consola en el momento: no se acepta por argumentos ni se guarda.
internal static class EntradaOperador
{
    public static CredencialOperador PedirCredencial()
    {
        Console.Write("Usuario de MariaDB con permiso para crear bases: ");
        var usuario = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Clave (no se muestra): ");
        var clave = LeerOculto();
        Console.WriteLine();
        return new CredencialOperador(usuario, clave);
    }

    public static bool Confirmar(string pregunta, string palabra)
    {
        Console.Write($"{pregunta} Escribe {palabra} para continuar: ");
        return string.Equals(Console.ReadLine()?.Trim(), palabra, StringComparison.Ordinal);
    }

    public static string PedirClaveNueva()
    {
        if (Console.IsInputRedirected) throw new ArgumentException("La contraseña nueva se escribe en una consola interactiva.");
        Console.Write("Nueva contraseña (no se muestra): ");
        var nueva = LeerOculto();
        Console.WriteLine();
        Console.Write("Repite la nueva contraseña: ");
        var repetida = LeerOculto();
        Console.WriteLine();
        if (!string.Equals(nueva, repetida, StringComparison.Ordinal)) throw new ArgumentException("Las contraseñas no coinciden. No se modificó la cuenta.");
        return nueva;
    }

    private static string LeerOculto()
    {
        if (Console.IsInputRedirected) return Console.ReadLine() ?? "";
        var texto = new StringBuilder();
        while (true)
        {
            var tecla = Console.ReadKey(intercept: true);
            if (tecla.Key == ConsoleKey.Enter) return texto.ToString();
            if (tecla.Key == ConsoleKey.Backspace)
            {
                if (texto.Length > 0) texto.Length--;
                continue;
            }
            if (char.IsControl(tecla.KeyChar)) continue;
            if (texto.Length >= 1024) throw new ArgumentException("La entrada supera la longitud permitida. Operación cancelada.");
            texto.Append(tecla.KeyChar);
        }
    }
}
