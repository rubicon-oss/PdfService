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
using System.ServiceModel;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Rubicon.PdfService.Contract;
using PageSize = iTextSharp.text.PageSize;

namespace Rubicon.PdfService
{
  [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, IncludeExceptionDetailInFaults = true)]
  public class PdfAService : PdfServiceBase, IPdfAServiceImplementation
  {
    protected override Font GetFont(string name, float size)
    {
      var font = FontFactory.GetFont(name, BaseFont.IDENTITY_H, BaseFont.EMBEDDED, size, Font.NORMAL, BaseColor.BLACK, true);
      return font;
    }

    protected override Result MergeModifiedContents(SourceDocument[] sourceFiles, Func<byte[], byte[]> modifyContents)
    {
      var result = base.MergeModifiedContents(sourceFiles, modifyContents);
      var pdfAConversionResult = ConvertToPdfA(result.Content);
      if (pdfAConversionResult != null && !pdfAConversionResult.IsPdfA)
        result.Content = pdfAConversionResult.Content;
      return result;
    }

    protected override void CreatePdf(Stream destination, Rectangle initialPageSize, int? margin, Action<Document, PdfWriter> modifyPdf)
    {
      initialPageSize = initialPageSize ?? PageSize.A4;
      using (var doc = new Document(initialPageSize))
      {
        if (margin.HasValue)
          doc.SetMargins(margin.Value, margin.Value, margin.Value, margin.Value);

        using (var writer = PdfAWriter.GetInstance(doc, destination, GetPdfAConformanceLevel()))
        {
          writer.CloseStream = false;
          writer.CreateXmpMetadata();
          doc.Open();
          EnsureIccProfile(writer);
          modifyPdf(doc, writer);
        }
      }
    }
    protected override void AdjustImage(Image img)
    {
      base.AdjustImage(img);
      // don't use SMask as it's not PDF/A conform
      img.Smask = false;
    }
  }
}
