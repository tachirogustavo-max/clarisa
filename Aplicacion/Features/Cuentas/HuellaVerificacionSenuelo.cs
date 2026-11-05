using Clarisa.Application.Abstractions.Seguridad;

namespace Clarisa.Application.Features.Cuentas;

public sealed class HuellaVerificacionSenuelo(IProtectorClaves protector, IGeneradorTokensSesion tokens)
{
    private readonly Lazy<string> huella = new(() => protector.Calcular(tokens.Generar().Valor));
    public string Valor => huella.Value;
    public void Preparar() => _ = huella.Value;
}
