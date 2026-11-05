using Clarisa.Api.Configuration;

if (!Microsoft.Extensions.Hosting.WindowsServices.WindowsServiceHelpers.IsWindowsService())
    Console.OutputEncoding = new System.Text.UTF8Encoding(false);
return await InicioServidor.EjecutarAsync(args);
