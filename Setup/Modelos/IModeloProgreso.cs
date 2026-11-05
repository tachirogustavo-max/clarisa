using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Clarisa.Setup.Modelos;

public interface IModeloProgreso : INotifyPropertyChanged
{
    string TituloProgreso { get; }
    string DescripcionProgreso { get; }
    ObservableCollection<PasoInstalacionVisible> Pasos { get; }
    PasoInstalacionVisible? PasoActual { get; }
    bool Finalizado { get; }
    bool MostrarDetalles { get; set; }
    IReadOnlyList<string> Indicaciones { get; }
}
