namespace Clarisa.Desktop.Presentation.Models;
public enum FiltroNotas
{
    Todas,
    Pendientes,
    Cambios,
    Errores
}

public sealed record OpcionFiltroNotas(FiltroNotas Valor, string Texto);
