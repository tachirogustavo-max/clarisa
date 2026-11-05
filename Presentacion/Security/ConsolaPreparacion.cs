using System.Text;
using Clarisa.Application.Features.Cuentas;

namespace Clarisa.Api.Security;

public interface IConsolaPreparacion
{
    bool EntradaRedirigida { get; }
    void Escribir(string texto);
    string? LeerLinea();
    string? LeerSecreto();
}

// La clave se lee tecla por tecla sin eco; nunca llega por argumentos, variables de entorno ni archivos.
public sealed class ConsolaSistema : IConsolaPreparacion
{
    public bool EntradaRedirigida => Console.IsInputRedirected;

    public void Escribir(string texto) => Console.Write(texto);

    public string? LeerLinea() => Console.ReadLine();

    public string? LeerSecreto()
    {
        var texto = new StringBuilder();
        while (true)
        {
            var tecla = Console.ReadKey(intercept: true);
            if (tecla.Key == ConsoleKey.Enter) break;
            if (tecla.Key == ConsoleKey.Backspace)
            {
                if (texto.Length > 0) texto.Length--;
                continue;
            }
            if (tecla.KeyChar == '\0') continue;
            if (texto.Length > PoliticaClaves.MaximoEntrada) return null;
            texto.Append(tecla.KeyChar);
        }
        Console.WriteLine();
        return texto.ToString();
    }
}
