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
using System.IO;
using System.Reflection;
using JetBrains.Annotations;

namespace Rubicon.PdfService.Server
{
  public class Arguments
  {
    public static bool TryParse(string[] args, out Arguments arguments)
    {
      arguments = null;
      if (args.Length < 2 || args.Length > 6 || args.Length == 3 || args.Length == 5)
        return false;

      if (!args[0].Equals("-t",StringComparison.OrdinalIgnoreCase))
        return false;

      arguments = new Arguments();
      arguments.IPdfServiceType = args[1];

      for (int i = 2; i <args.Length; i += 2)
      {
        if (args[i].Equals("-s", StringComparison.OrdinalIgnoreCase))
          arguments.ServiceName = args[i + 1];
        else if (args[i].Equals("-b", StringComparison.OrdinalIgnoreCase))
          arguments.BindingName = args[i + 1];
        else
          return false;
      }

      return true;
    }

    [CanBeNull]
    public string BindingName { get; set; }

    [CanBeNull]
    public string ServiceName { get; set; }

    [NotNull]
    public string IPdfServiceType { get; set; }

    public static void WriterUsages(TextWriter textWriter)
    {
      textWriter.WriteLine($"{Assembly.GetExecutingAssembly().GetName().Name} -t TYPE [-s ServiceName] [-b BindingName]");
    }
  }
}