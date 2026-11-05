using System.IO;
using System.Windows.Media;
using Clarisa.Desktop.Presentation.Documents;
using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Presentation.Models;

// Vista previa del boletín: el mismo PDF que genera el servidor, dibujado como imagen. Ese mismo documento es el que se guarda o se
// imprime, así que lo que se ve es lo que sale. Si no se puede dibujar, la pantalla muestra la tabla con los mismos datos.
public partial class ModeloBoletines
{
    private byte[]? documentoConsultado;

    public IReadOnlyList<ImageSource> Paginas { get; private set; } = [];
    public bool TieneVistaPrevia => Paginas.Count > 0;
    public bool SinVistaPrevia => TieneBoletin && !TieneVistaPrevia;

    private async Task CargarVistaPreviaAsync(BoletinRemoto consultado, Guid? lapsoId, CancellationToken ct)
    {
        if (documentos is null) return;
        var respuesta = await documentos.BoletinAsync(consultado.InscripcionId, lapsoId, ct);
        if (!ReferenceEquals(Boletin, consultado) || respuesta.Datos is not { } contenido) return;
        try
        {
            var paginas = await PaginasPdf.DibujarAsync(contenido, ct);
            if (!ReferenceEquals(Boletin, consultado)) return;
            documentoConsultado = contenido;
            Paginas = paginas;
        }
        catch (Exception error) when (error is InvalidOperationException or IOException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            System.Diagnostics.Trace.TraceWarning("No se pudo dibujar la vista previa del boletín: {0}.", error.GetType().Name);
        }
        NotificarVistaPrevia();
    }

    // Para capturas de pantalla con un PDF de ejemplo, sin servidor.
    internal void MostrarDocumento(byte[] contenido, IReadOnlyList<ImageSource> paginas)
    {
        documentoConsultado = contenido;
        Paginas = paginas;
        NotificarVistaPrevia();
    }

    private void OlvidarVistaPrevia()
    {
        documentoConsultado = null;
        Paginas = [];
        NotificarVistaPrevia();
    }

    private void NotificarVistaPrevia()
    {
        Notificar(nameof(Paginas));
        Notificar(nameof(TieneVistaPrevia));
        Notificar(nameof(SinVistaPrevia));
    }
}
