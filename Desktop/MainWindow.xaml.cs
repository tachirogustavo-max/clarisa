using System.Windows;
using System.Windows.Input;
using Clarisa.Desktop.Presentation.Behaviors;
using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Views;

namespace Clarisa.Desktop;
public partial class MainWindow : Window
{
    private bool cierreAutorizado;
    public MainWindow()
    {
        InitializeComponent();
        CommandBindings.Add(new CommandBinding(ComandosVentana.MostrarGuia,
            (_, _) => (Content as VistaEscritorio)?.MostrarGuia(),
            (_, e) => e.CanExecute = Content is VistaEscritorio { PuedeMostrarGuia: true }));
        CommandBindings.Add(new CommandBinding(ComandosVentana.VolverMaterias,
            (_, _) => (Content as VistaEscritorio)?.VolverMaterias(),
            (_, e) => e.CanExecute = Content is VistaEscritorio { PuedeVolverMaterias: true }));
        if (!System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
        {
            var area = SystemParameters.WorkArea;
            MinWidth = Math.Min(MinWidth, area.Width);
            MinHeight = Math.Min(MinHeight, area.Height);
            Width = Math.Min(Width, area.Width);
            Height = Math.Min(Height, area.Height);
        }
        Closing += (_, e) =>
        {
            if (!cierreAutorizado && Content is VistaPlanilla planilla)
                e.Cancel = !planilla.SolicitarSalida(() =>
                {
                    cierreAutorizado = true;
                    Close();
                });
            if (!cierreAutorizado && Content is VistaEscritorio escritorio)
                e.Cancel = !escritorio.SolicitarSalida(() =>
                {
                    cierreAutorizado = true;
                    Close();
                });
        };
        Closed += (_, _) =>
        {
            (DataContext as ModeloAcceso)?.Cancelar();
            (DataContext as ModeloEscritorio)?.Cancelar();
            if (Content is FrameworkElement { DataContext: ModeloPlanilla planilla })
                planilla.Cancelar();
            if (Content is FrameworkElement { DataContext: ModeloDirector director })
                director.Cancelar();
        };
    }
}
