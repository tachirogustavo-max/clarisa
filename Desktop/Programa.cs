namespace Clarisa.Desktop;

// Punto de entrada. Con «--dibujar-pdf» el programa solo convierte un PDF en imágenes para la vista previa y termina, sin abrir
// ventanas: el lector de PDF de Windows no puede usarse dentro del proceso con interfaz sin que este falle al cerrarse.
public static class Programa
{
    [STAThread]
    public static int Main(string[] argumentos)
    {
        if (argumentos.Length == 3 && argumentos[0] == Presentation.Documents.DibujoPdf.Opcion)
            return Presentation.Documents.DibujoPdf.Ejecutar(argumentos[1], argumentos[2]);
        var aplicacion = new App();
        aplicacion.InitializeComponent();
        return aplicacion.Run();
    }
}
