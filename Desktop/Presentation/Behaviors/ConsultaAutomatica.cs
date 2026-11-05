using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Clarisa.Desktop.Presentation.Models;

namespace Clarisa.Desktop.Presentation.Behaviors;
public static class ConsultaAutomatica
{
    public static readonly DependencyProperty ActivaProperty = DependencyProperty.RegisterAttached("Activa", typeof(bool), typeof(ConsultaAutomatica), new(false, Cambiar));
    private static readonly DependencyProperty EstadoProperty = DependencyProperty.RegisterAttached("Estado", typeof(ConsultaDiferida), typeof(ConsultaAutomatica));
    public static bool GetActiva(DependencyObject objeto) => (bool)objeto.GetValue(ActivaProperty);
    public static void SetActiva(DependencyObject objeto, bool valor) => objeto.SetValue(ActivaProperty, valor);
    private static void Cambiar(DependencyObject objeto, DependencyPropertyChangedEventArgs e)
    {
        if (objeto.GetValue(EstadoProperty) is ConsultaDiferida anterior) anterior.Dispose();
        objeto.ClearValue(EstadoProperty);
        if (e.NewValue is true && objeto is Control control) objeto.SetValue(EstadoProperty, new ConsultaDiferida(control));
    }
    private sealed class ConsultaDiferida : IDisposable
    {
        private readonly Control control;
        private readonly ConsultaProgramada espera;
        private object? contexto;
        public ConsultaDiferida(Control control)
        {
            this.control = control;
            espera = new ConsultaProgramada(control.Dispatcher, () => control.IsLoaded && ReferenceEquals(contexto, control.DataContext)
                ? contexto as IListadoCatalogo : null);
            control.Unloaded += Descargar;
            control.DataContextChanged += CambiarContexto;
            control.PreviewKeyDown += Tecla;
            if (control is TextBox texto) texto.TextChanged += Escribir;
            if (control is ComboBox opciones) opciones.SelectionChanged += Seleccionar;
            if (control is CheckBox casilla) { casilla.Checked += Marcar; casilla.Unchecked += Marcar; }
        }
        private void Programar()
        {
            if (!control.IsLoaded || !control.IsEnabled) return;
            contexto = control.DataContext;
            espera.Programar();
        }
        private void Escribir(object sender, TextChangedEventArgs e) => Programar();
        private void Seleccionar(object sender, SelectionChangedEventArgs e) => Programar();
        private void Marcar(object sender, RoutedEventArgs e) => Programar();
        private void Descargar(object sender, RoutedEventArgs e) => espera.Detener();
        private void CambiarContexto(object sender, DependencyPropertyChangedEventArgs e) => espera.Detener();
        private void Tecla(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) espera.Detener(); }
        public void Dispose()
        {
            espera.Dispose();
            control.Unloaded -= Descargar;
            control.DataContextChanged -= CambiarContexto;
            control.PreviewKeyDown -= Tecla;
            if (control is TextBox texto) texto.TextChanged -= Escribir;
            if (control is ComboBox opciones) opciones.SelectionChanged -= Seleccionar;
            if (control is CheckBox casilla) { casilla.Checked -= Marcar; casilla.Unchecked -= Marcar; }
        }
    }
}
