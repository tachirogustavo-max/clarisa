using Clarisa.Desktop.Presentation.Models;
using Clarisa.Desktop.Services;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Configuration;
internal sealed class PreparacionCatalogos(IClienteOpciones opciones)
{
    public async Task<string?> PrepararAsync(object modelo, CancellationToken ct)
    {
        var tipos = modelo switch
        {
            ModeloSecciones or ModeloLapsos or ModeloMaterias => new[]
            {
                TipoOpciones.Anios
            },
            ModeloInscripciones => [TipoOpciones.Anios, TipoOpciones.Estudiantes, TipoOpciones.Secciones],
            ModeloAsignaciones => [TipoOpciones.Anios, TipoOpciones.Secciones, TipoOpciones.Materias, TipoOpciones.Docentes],
            ModeloUsuarios => [TipoOpciones.Docentes],
            ModeloReportes { EsAdministracion: false } => [TipoOpciones.Anios, TipoOpciones.Secciones, TipoOpciones.Materias, TipoOpciones.Lapsos],
            ModeloReportes => [TipoOpciones.Anios, TipoOpciones.Secciones, TipoOpciones.Materias, TipoOpciones.Docentes, TipoOpciones.Lapsos],
            _ => Array.Empty<TipoOpciones>()};
        var respuestas = await Task.WhenAll(tipos.Select(tipo => opciones.ConsultarAsync(tipo, null, null, ct)));
        ct.ThrowIfCancellationRequested();
        var error = respuestas.FirstOrDefault(r => r.Datos is null || r.Error is not null);
        if (error is not null)
            return error.Error ?? "No se pudieron cargar las opciones del formulario.";
        var datos = tipos.Select((tipo, indice) => (tipo, indice)).ToDictionary(t => t.tipo, t => respuestas[t.indice].Datos!);
        switch (modelo)
        {
            case ModeloMaterias materias:
                materias.EstablecerAnios(datos[TipoOpciones.Anios]);
                break;
            case ModeloSecciones secciones:
                secciones.EstablecerAnios(datos[TipoOpciones.Anios]);
                break;
            case ModeloLapsos lapsos:
                lapsos.EstablecerAnios(datos[TipoOpciones.Anios]);
                break;
            case ModeloInscripciones inscripciones:
                inscripciones.EstablecerOpcionesAnio(datos[TipoOpciones.Anios]);
                inscripciones.EstablecerOpciones(datos[TipoOpciones.Estudiantes], datos[TipoOpciones.Secciones]);
                break;
            case ModeloAsignaciones asignaciones:
                asignaciones.EstablecerOpcionesAnio(datos[TipoOpciones.Anios]);
                asignaciones.EstablecerOpciones(datos[TipoOpciones.Secciones], datos[TipoOpciones.Materias], datos[TipoOpciones.Docentes]);
                break;
            case ModeloUsuarios usuarios:
                usuarios.EstablecerDocentes(datos[TipoOpciones.Docentes]);
                break;
            case ModeloReportes reportes:
                foreach (var(tipo, lista)in datos)
                    reportes.EstablecerOpciones(tipo, lista);
                break;
        }

        return null;
    }
}
