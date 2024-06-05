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
using iTextSharp.text;
using iTextSharp.text.pdf;
using Rubicon.PdfService.Contract;

namespace Rubicon.PdfService
{
  public class PdfService : PdfServiceBase
  {
    public PdfService(string iccProfilePath, string iccProfileName, PdfAVersion? pdfAVersion)
      : base(iccProfilePath, iccProfileName, pdfAVersion)
    {
    }

    // ReSharper disable once UnusedParameter.Local
    protected override void CreatePdf(Stream destination, Rectangle initialPageSize, int? margin, Action<Document, PdfWriter> modifyPdf)
    {
      initialPageSize = initialPageSize ?? iTextSharp.text.PageSize.A4;
      using (var doc = new Document(initialPageSize))
      {
        if (margin.HasValue)
          doc.SetMargins(margin.Value, margin.Value, margin.Value, margin.Value);

        /*
         We need to handle exceptions inside this using block,
         because, unfortunately, disposing the writer can potentially throw a second exception itself,
         which would hide our first exception if unhandled.
        */
        using (var writer = PdfWriter.GetInstance(doc, destination))
        {
          writer.CloseStream = false;
          doc.Open();
          modifyPdf(doc, writer);
        }
      }
    }
  }
}