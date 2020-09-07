// Copyright (c) RUBICON IT GmbH
//
//This program is free software; you can redistribute it and/or modify
//it under the terms of the GNU Affero General Public License version 3
//as published by the Free Software Foundation.
//
//This program is distributed in the hope that it will be useful, but
//WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
//or FITNESS FOR A PARTICULAR PURPOSE. 
//
//See the GNU Affero General Public License for more details.
//
//You should have received a copy of the GNU Affero General Public License
//along with this program; if not, see <http://www.gnu.org/licenses/>.

using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using JetBrains.Annotations;
using Rubicon.PdfService.Contract;

namespace Rubicon.PdfService.Server
{
  public class Program
  {
    public static int Main(string[] args)
    {
      if (!Arguments.TryParse(args, out var arguments))
      {
        Arguments.WriterUsages(Console.Error);
        return -1;
      }

      try
      {
        var namedPipeBinding = GetNamedPipeBinding(arguments.BindingName);
        var serviceUri = GetServiceUri(arguments.ServiceName);
        var serviceType = GetServiceType(arguments.IPdfServiceType);
        var serviceHost = CreateServiceHost(serviceUri, namedPipeBinding, serviceType);

        Console.WriteLine("Starting server...");
        serviceHost.Open();

        var resetEvent = new ManualResetEvent(false);
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
          Console.WriteLine("Shutting down server...");
          serviceHost.Close();
          resetEvent.Set();
        };
        
        Console.WriteLine($"listening on {serviceUri} for connections");
        Console.WriteLine($"Press CTRL-C to shutdown server");

        resetEvent.WaitOne();
        return 0;
      }
      catch (Exception exception)
      {
        Console.Error.WriteLine(exception);
        return -1;
      }
    }

    private static Type GetServiceType([NotNull]string pdfServiceType)
    {
      var type = GetType(pdfServiceType);
      if(type == null || !typeof(IPdfService).IsAssignableFrom(type))
        throw new InvalidOperationException($"Could not load type \"{pdfServiceType}\" or type does not implement {nameof(IPdfService)}");

      return type;
    }

    [CanBeNull]
    private static Type GetType(string pdfServiceType)
    {
      try
      {
        return Type.GetType(pdfServiceType);
      }
      catch (Exception e)
      {
        throw new InvalidOperationException($"Could not load type \"{pdfServiceType}\"", e);
      }
    }

    [NotNull]
    private static Binding GetNamedPipeBinding(string argumentsBindingName)
    {
      if(argumentsBindingName != null)
        return new NetNamedPipeBinding(argumentsBindingName);

      return new NetNamedPipeBinding();
    }

    [NotNull]
    public static Uri GetServiceUri([CanBeNull]string serviceName)
    {
      if(serviceName == null)
        return new UriBuilder(Uri.UriSchemeNetPipe, "localhost", -1, "PdfService_" + Guid.NewGuid()).Uri;

      return new UriBuilder(Uri.UriSchemeNetPipe, "localhost", -1, serviceName).Uri;
    }

    [NotNull]
    private static ServiceHost CreateServiceHost([NotNull] Uri serviceUri, [NotNull] Binding namedPipeBinding, [NotNull]Type serviceType)
    {
      var host = new ServiceHost(serviceType);
      host.AddServiceEndpoint(typeof(IPdfService), namedPipeBinding, serviceUri);
      return host;
    }
  }
}
