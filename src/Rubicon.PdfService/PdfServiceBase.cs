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
using System.Collections;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using iTextSharp.text;
using iTextSharp.text.exceptions;
using iTextSharp.text.pdf;
using iTextSharp.xmp.impl;
using JetBrains.Annotations;
using Rubicon.PdfService.Contract;

namespace Rubicon.PdfService
{
  public abstract class PdfServiceBase : IPdfService
  {
    private readonly string _iccProfilePath;
    private readonly string _iccProfileName;
    private readonly PdfAVersion? _pdfAVersion;

    static PdfServiceBase()
    {
      FontFactory.RegisterDirectory(Environment.GetFolderPath(Environment.SpecialFolder.Fonts));
    }

    private static readonly string s_rgbIccProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"spool\drivers\color\sRGB Color Space Profile.icm");

    private const string c_kidsBookmarks = "Kids";
    private const string c_rgbIccProfileName = "sRGB IEC61966-2.1";
    private const string c_iccProfileUrl = "http://www.color.org";

    protected PdfServiceBase(string iccProfilePath, string iccProfileName, PdfAVersion? pdfAVersion)
    {
      _iccProfilePath = iccProfilePath;
      _iccProfileName = iccProfileName;
      _pdfAVersion = pdfAVersion;
    }
    
    /// <summary>
    ///   Converts a Pdf file to a Pdf/A-1b
    /// </summary>
    /// <param name="pdfFile">Image byte array</param>
    /// <returns>Returns a PdfAConversionResult</returns>
    public virtual PdfAConversionResult ConvertToPdfA(byte[] pdfFile)
    {
      var info = GetPdfInfo(pdfFile);
      if (info.ConformanceLevel == info.ConfiguredConformanceLevel && info.ConfiguredConformanceLevel != null)
        return new PdfAConversionResult { IsPdfA = true, PageCount = info.PageCount };

      return ExecWithMonitoring(() => ConvertToPdfAInternal(pdfFile));
    }

    public virtual PdfInfo GetPdfInfo(byte[] pdfFile)
    {
      return ExecWithMonitoring(() => GetPdfInfoInternal(pdfFile));
    }

    public virtual byte[] ConvertImage(byte[] imageData, Contract.PageSize pageSize, int? margins)
    {
      return ExecWithMonitoring(() => ConvertImageInternal(imageData, pageSize, margins));
    }

    public virtual Result Merge(SourceDocument[] pdfFiles)
    {
      return ExecWithMonitoring(() => MergeModifiedContents(pdfFiles, c => c));
    }

    public virtual Result ResizeMerge(SourceDocument[] pdfFiles, Contract.PageSize pageSize, bool isLandscape)
    {
      return ExecWithMonitoring(
        () => MergeModifiedContents(pdfFiles, c => Resize(c, pageSize, isLandscape, 10)));
    }

    public virtual byte[] Resize(byte[] pdfFile, Contract.PageSize pageSize, bool isLandscape, int margin)
    {
      return ExecWithMonitoring(() => ResizeInternal(pdfFile, pageSize, isLandscape, margin));
    }

    public virtual byte[] CreateNewPdfWithCenteredText(string[] lines, string fontName, float fontSize, Contract.PageSize pageSize, bool isLandscape)
    {
      return ExecWithMonitoring(() => CreateNewPdfWithCenteredTextInternal(lines, fontName, fontSize, pageSize, isLandscape));
    }

    public byte[] AddPageNumbers(
      byte[] pdfFile,
      int pagesToSkipBeforeNumbering,
      int firstPageNumberToUse,
      int? totalPageCount,
      string fontName,
      float fontSize,
      float margin)
    {
      return
        ExecWithMonitoring(
          () => AddPageNumbersInternal(pdfFile, pagesToSkipBeforeNumbering, firstPageNumberToUse, totalPageCount, fontName, fontSize, margin));
    }

    public int GetNumberOfPages(byte[] pdfFile)
    {
      return ExecWithMonitoring(() => GetNumberOfPagesInternal(pdfFile));
    }

    private int GetNumberOfPagesInternal(byte[] pdfFile)
    {
      CheckNotNullOrEmpty(pdfFile, nameof(pdfFile));

      using (var stream = new MemoryStream(pdfFile))
      {
        return GetNumberOfPagesInternal(stream);
      }
    }

    public byte[] AddOverlay(
      byte[] pdfFile,
      string pageOverlayText,
      OverlayPlacementVertical verticalPlacement,
      OverlayPlacementHorizontal horizontalPlacement,
      string fontName,
      float fontSize,
      float margin)
    {
      return ExecWithMonitoring(() => AddOverlayInternal(pdfFile, pageOverlayText, verticalPlacement, horizontalPlacement, fontName, fontSize, margin));
    }

    private byte[] AddOverlayInternal(
      byte[] pdfFile,
      string pageOverlayText,
      OverlayPlacementVertical verticalPlacement,
      OverlayPlacementHorizontal horizontalPlacement,
      string fontName,
      float fontSize,
      float margin)
    {
      using (var contentStream = new MemoryStream(pdfFile))
      {
        var reader = new PdfReader(contentStream);
        using (var outputStream = new MemoryStream())
        {
          using (var stamper = new PdfStamper(reader, outputStream))
          {
            stamper.Writer.CloseStream = false;
            stamper.RotateContents = true;
            for (var i = 1; i <= reader.NumberOfPages; i++)
            {
              var text = pageOverlayText;
              if (string.IsNullOrEmpty(pageOverlayText))
                continue;

              AddOverlayInternal(text, verticalPlacement, horizontalPlacement, fontName, fontSize, margin, reader, i, stamper);
            }
            stamper.Close();
          }
          return outputStream.ToArray();
        }
      }
    }

    protected virtual void AddCenteredTextOnNewPage(Document document, PdfWriter writer, string[] lines, string fontName, float fontSize)
    {
      CheckNotNull(document, "document");
      CheckNotNull(writer, "writer");
      CheckNotNullOrEmpty(lines, "lines");
      CheckNotNullOrEmpty(fontName, "fontName");

      document.NewPage();

      var str = string.Join("\r\n", lines);

      var pdfPtable = new PdfPTable(1);
      pdfPtable.TotalWidth = document.PageSize.Width - document.LeftMargin - document.RightMargin;
      pdfPtable.HorizontalAlignment = Element.ALIGN_CENTER;
      pdfPtable.LockedWidth = true;

      var paragraph = new Paragraph(str, GetFont(fontName, fontSize));
      paragraph.Alignment = Element.ALIGN_CENTER;

      var cell = new PdfPCell(paragraph);
      cell.FixedHeight = document.PageSize.Height - document.BottomMargin - document.TopMargin;
      cell.VerticalAlignment = Element.ALIGN_MIDDLE;
      cell.HorizontalAlignment = Element.ALIGN_CENTER;

      cell.Border = 0;
      pdfPtable.AddCell(cell);
      document.Add(pdfPtable);
      document.Close();
    }

    protected void CheckNotNull<T>(T parameter, string parameterName) where T : class
    {
      if (parameter == null)
        throw new ArgumentNullException(parameterName);
    }

    protected void CheckNotNullOrEmpty<T>(T parameter, string parameterName) where T : class, ICollection
    {
      CheckNotNull(parameter, parameterName);
      if (parameter.Count == 0)
        throw new ArgumentException("Parameter cannot be empty.", parameterName);
    }

    protected void CheckNotNullOrEmpty(string parameter, string parameterName)
    {
      CheckNotNull(parameter, parameterName);
      if (parameter.Length == 0)
        throw new ArgumentException("Parameter cannot be empty.", parameterName);
    }

    protected virtual void ConvertImageToPdf(string imageTempFile, string pdfTempFile, Rectangle effectivePageSize, int? margins)
    {
      using (var fileStream = File.Open(pdfTempFile, FileMode.OpenOrCreate, FileAccess.ReadWrite))
      {
        using (var imageFileStream = File.Open(imageTempFile, FileMode.Open, FileAccess.Read))
        {
          // ReSharper disable once AccessToDisposedClosure
          CreatePdf(fileStream, null, margins, (doc, writer) => ConvertImageToPdfInternal(imageFileStream, effectivePageSize, writer, doc));
        }
      }
    }

    private void ConvertImageToPdfInternal(FileStream imageStream, Rectangle effectivePageSize, PdfWriter writer, Document document)
    {
      var image = System.Drawing.Image.FromStream(imageStream);

      if (ImageFormat.Tiff.Equals(image.RawFormat))
      {
        var frameDimension = new FrameDimension(image.FrameDimensionsList[0]);
        var frameCount = image.GetFrameCount(frameDimension) - 1;

        for (var i = 0; i <= frameCount; ++i)
        {
          image.SelectActiveFrame(FrameDimension.Page, i);
          var img = Image.GetInstance(image, ImageFormat.Tiff);
          AddImage(effectivePageSize, document, img);
        }
      }
      else
      {
        imageStream.Position = 0;
        var img = Image.GetInstance(Image.GetInstance(ReadAllBytes(imageStream)));
        AddImage(effectivePageSize, document, img);
      }

      document.Close();
    }

    private byte[] CreateNewPdfWithCenteredTextInternal(
      string[] lines,
      string fontName,
      float fontSize,
      Contract.PageSize pageSize,
      bool isLandscape)
    {
      CheckNotNullOrEmpty(lines, "lines");
      CheckNotNullOrEmpty(fontName, "fontName");

      using (var memoryStream = new MemoryStream())
      {
        var initialPageSize = PageSizeConverter.Convert(pageSize, isLandscape);
        CreatePdf(
          memoryStream,
          initialPageSize,
          null,
          (doc, writer) => AddCenteredTextOnNewPage(doc, writer, lines, fontName, fontSize));
        return memoryStream.ToArray();
      }
    }

    protected abstract void CreatePdf(Stream destination, Rectangle initialPageSize, int? margin, Action<Document, PdfWriter> modifyPdf);

    protected void DeleteFile(string path)
    {
      try
      {
        File.Delete(path);
      }
      catch (IOException)
      {
      }
    }

    protected void EnsureIccProfile(PdfAWriter pdfWriter)
    {
      var rgbIccProfilePath = s_rgbIccProfilePath;
      if (!string.IsNullOrEmpty(_iccProfilePath))
        rgbIccProfilePath = _iccProfilePath;

      var rgbIccProfileName = c_rgbIccProfileName;
      if (!string.IsNullOrEmpty(_iccProfileName))
        rgbIccProfileName = _iccProfileName;

      var icc = ICC_Profile.GetInstance(rgbIccProfilePath);
      pdfWriter.SetOutputIntents("Custom", "", c_iccProfileUrl, rgbIccProfileName, icc);
    }

    private T ExecWithMonitoring<T>(Func<T> func)
    {
      try
      {
        return func();
      }
      catch (BadPasswordException)
      {
        Console.WriteLine(ErrorMessages.DocumentIsPasswordProtected);
        throw;
      }
      catch (InvalidPdfException)
      {
        Console.WriteLine(ErrorMessages.DocumentIsInvalid);
        throw;
      }
    }

    protected virtual Font GetFont(string name, float size)
    {
      var font = FontFactory.GetFont(name, BaseFont.IDENTITY_H, false, size, Font.NORMAL, BaseColor.BLACK, true);
      return font;
    }

    protected float GetHorizontalPosition(OverlayPlacementHorizontal horizontalPlacement, float margin, Rectangle pageSize, Chunk chunk)
    {
      float llx = 0;
      switch (horizontalPlacement)
      {
        case OverlayPlacementHorizontal.Left:
          llx = margin;
          break;
        case OverlayPlacementHorizontal.Right:
          llx = pageSize.Width - margin - chunk.GetWidthPoint();
          break;
        case OverlayPlacementHorizontal.Center:
          llx = (pageSize.Width - margin - chunk.GetWidthPoint()) / 2;
          break;
      }
      return llx;
    }

    protected int GetNumberOfPagesInternal(Stream stream)
    {
      CheckNotNull(stream, "stream");

      int numberOfPages;
      using (var reader = new PdfReader(stream))
      {
        numberOfPages = reader.NumberOfPages;
        reader.Close();
      }
      return numberOfPages;
    }

    protected float GetVerticalPosition(OverlayPlacementVertical verticalPlacement, float margin, Rectangle pageSize)
    {
      float lly = 0;
      switch (verticalPlacement)
      {
        case OverlayPlacementVertical.Top:
          lly = pageSize.Height - margin;
          break;
        case OverlayPlacementVertical.Bottom:
          lly = margin;
          break;
        case OverlayPlacementVertical.Center:
          lly = (pageSize.Height - margin) / 2;
          break;
      }
      return lly;
    }

    protected bool IsLandscape([NotNull]Rectangle r)
    {
      return r.Width > r.Height;
    }

    protected virtual Result MergeModifiedContents(SourceDocument[] sourceFiles, Func<byte[], byte[]> modifyContents)
    {
      CheckNotNullOrEmpty(sourceFiles, "sourceFiles");
      modifyContents = modifyContents ?? (c => c);

      var destinationFilePath = Path.GetTempFileName();
     
      List<SourceDocumentInfo> sourceDocumentInfos = null;
      try
      {
        sourceDocumentInfos = GetSourceDocumentInfos(sourceFiles, modifyContents);

        int totalPagesMerged;
        using (var destinationFileStream = File.Open(destinationFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
        {
          try
          {
            OpenInputStreams(sourceDocumentInfos);

            totalPagesMerged = MergeTo(sourceDocumentInfos, destinationFileStream);
          }
          finally
          {
            CloseInputStreams(sourceDocumentInfos);
          }
        }
        return new Result { Content = File.ReadAllBytes(destinationFilePath), PageCount = totalPagesMerged };
      }
      finally
      {
        DeleteFile(destinationFilePath);

        if (sourceDocumentInfos != null)
        {
          foreach (var sourceFile in sourceDocumentInfos)
            DeleteFile(sourceFile.TempFileName);
        }
      }
    }

    private static void OpenInputStreams(List<SourceDocumentInfo> sourceDocumentInfos)
    {
      foreach (var sdi in sourceDocumentInfos)
        sdi.InputStream = File.Open(sdi.TempFileName, FileMode.Open);
    }

    private void AddImage(Rectangle effectivePageSize, Document document, [NotNull]Image img)
    {
      if (IsLandscape(img))
        document.SetPageSize(effectivePageSize.Rotate());

      document.NewPage();


      if (img.Height > document.PageSize.Height - document.TopMargin - document.BottomMargin ||
          img.Width > document.PageSize.Width - document.LeftMargin - document.RightMargin)
      {
        img.ScaleToFit(
          document.PageSize.Width - document.LeftMargin - document.RightMargin,
          document.PageSize.Height - document.TopMargin - document.BottomMargin);
      }

      AdjustImage(img);

      document.Add(img);
    }

    protected virtual void AdjustImage(Image img)
    {
    }

    private List<Dictionary<string, object>> AddKidsCollection(Dictionary<string, object> currentBookmark)
    {
      var collection = new List<Dictionary<string, object>>();
      currentBookmark.Add(c_kidsBookmarks, collection);
      return collection;
    }

    private void AddOverlayInternal(
      string text,
      OverlayPlacementVertical verticalPlacement,
      OverlayPlacementHorizontal horizontalPlacement,
      string fontName,
      float fontSize,
      float margin,
      PdfReader reader,
      int pageNo,
      PdfStamper stamper)
    {
      var pageSize = reader.GetPageSizeWithRotation(pageNo);
      var canvas = stamper.GetOverContent(pageNo);
      var font = FontFactory.GetFont(fontName, fontSize, BaseColor.BLACK);

      var chunk = new Chunk(text, font);
      chunk.SetBackground(BaseColor.WHITE);
      var phrase = new Phrase(chunk);

      var llx = GetHorizontalPosition(horizontalPlacement, margin, pageSize, chunk);
      var lly = GetVerticalPosition(verticalPlacement, margin, pageSize);

      ColumnText.ShowTextAligned(canvas, Element.ALIGN_LEFT, phrase, llx, lly, 0);
    }

    private byte[] AddOverlays(
      byte[] pdfFile,
      Func<int, string> pageOverlayText,
      OverlayPlacementVertical verticalPlacement,
      OverlayPlacementHorizontal horizontalPlacement,
      string fontName,
      float fontSize,
      float margin)
    {
      using (var outputStream = File.Create(Path.GetTempFileName(), 4096, FileOptions.RandomAccess | FileOptions.DeleteOnClose))
      {
        using (var contentStream = new MemoryStream(pdfFile))
        {
          var reader = new PdfReader(contentStream);
          using (var stamper = new PdfStamper(reader, outputStream))
          {
            stamper.Writer.CloseStream = false;
            stamper.RotateContents = true;
            for (var i = 1; i <= reader.NumberOfPages; i++)
            {
              var text = pageOverlayText(i - 1);
              if (string.IsNullOrEmpty(text))
                continue;

              AddOverlayInternal(text, verticalPlacement, horizontalPlacement, fontName, fontSize, margin, reader, i, stamper);
            }
            stamper.Close();
          }
        }

        outputStream.Seek(0, SeekOrigin.Begin);
        return outputStream.ReadAllBytes();
      }
    }

    private byte[] AddPageNumbersInternal(
      byte[] content,
      int pagesToSkipBeforeNumbering,
      int firstPageNumberToUse,
      int? totalPageCount,
      string fontName,
      float fontSize,
      float margin)
    {
      var maxPageNumber = 0;
      if (totalPageCount.HasValue)
        maxPageNumber = totalPageCount.Value - pagesToSkipBeforeNumbering + firstPageNumberToUse - 1;

      return AddOverlays(
        content,
        page => GetOverlayText(pagesToSkipBeforeNumbering, firstPageNumberToUse, totalPageCount, page, maxPageNumber),
        OverlayPlacementVertical.Bottom,
        OverlayPlacementHorizontal.Center,
        fontName,
        fontSize,
        margin);
    }

    private static string GetOverlayText(int pagesToSkipBeforeNumbering, int firstPageNumberToUse, int? totalPageCount, int page, int maxPageNumber)
    {
      if (page < pagesToSkipBeforeNumbering)
        return null;

      var pageNumber = page - pagesToSkipBeforeNumbering + firstPageNumberToUse;
      return totalPageCount.HasValue
        ? string.Format("{0} / {1}", pageNumber, maxPageNumber)
        : pageNumber.ToString(CultureInfo.InvariantCulture);
    }

    private void CloseInputStreams(IEnumerable<SourceDocumentInfo> sourceDocumentInfos)
    {
      foreach (var sourceStream in sourceDocumentInfos)
      {
        try
        {
          if (sourceStream.InputStream != null)
            sourceStream.InputStream.Close();
        }
        catch (IOException)
        {
        }
      }
    }

    private byte[] ConvertImageInternal(byte[] imageData, Contract.PageSize pageSize, int? margins)
    {
      CheckNotNullOrEmpty(imageData, "imageData");

      var effectivePageSize = PageSizeConverter.Convert(pageSize, false);
      var imageTempFile = Path.GetTempFileName();
      var pdfTempFile = Path.GetTempFileName();

      try
      {
        File.WriteAllBytes(imageTempFile, imageData);
        ConvertImageToPdf(imageTempFile, pdfTempFile, effectivePageSize, margins);
        return File.ReadAllBytes(pdfTempFile);
      }
      finally
      {
        DeleteFile(imageTempFile);
        DeleteFile(pdfTempFile);
      }
    }

    protected PdfAConformanceLevel GetPdfAConformanceLevel()
    {
      return _pdfAVersion switch
      {
        PdfAVersion.PdfA1b => PdfAConformanceLevel.PDF_A_1B,
        PdfAVersion.PdfA2b => PdfAConformanceLevel.PDF_A_2B,
        _ => throw new InvalidOperationException($"Unknown value: \"{_pdfAVersion}\".")
      };
    }

    private PdfAConversionResult ConvertToPdfAInternal(byte[] pdfFile)
    {
      using (var outStream = File.Create(Path.GetTempFileName(), 4096, FileOptions.RandomAccess | FileOptions.DeleteOnClose))
      {
        int pageCount;
        using (var inStream = new MemoryStream(pdfFile))
        {
          using (var reader = new PdfReader(inStream))
          {
            using (var doc = new Document())
            {
              using (var writer = PdfAWriter.GetInstance(doc, outStream, GetPdfAConformanceLevel()))
              {
                writer.CloseStream = false;
                writer.CreateXmpMetadata();
                doc.Open();

                reader.ConsolidateNamedDestinations();
                reader.MakeRemoteNamedDestinationsLocal();
                pageCount = reader.NumberOfPages;
                for (var i = 1; i <= reader.NumberOfPages; i++)
                {
                  var pageSizeWithRotation = reader.GetPageSizeWithRotation(i);
                  doc.SetPageSize(pageSizeWithRotation);
                  doc.SetMargins(0, 0, 0, 0);
                  doc.NewPage();
                  doc.SetPageSize(pageSizeWithRotation);
                  doc.SetMargins(0, 0, 0, 0);
                  var page = writer.GetImportedPage(reader, i);
                  var pageRotation = reader.GetPageRotation(i);
                  var pageWidth = pageSizeWithRotation.Width;
                  var pageHeight = pageSizeWithRotation.Height;
                  switch (pageRotation)
                  {
                    case 0:
                      writer.DirectContent.AddTemplate(page, 0, 0);
                      //writer.DirectContent.AddTemplate(page, 1.0F, 0, 0, 1.0F, 0, 0);
                      break;

                    case 90:
                      writer.DirectContent.AddTemplate(page, 0, -1f, 1f, 0, 0, pageHeight);
                      break;

                    case 180:
                      writer.DirectContent.AddTemplate(page, -1f, 0, 0, -1f, pageWidth, pageHeight);
                      break;

                    case 270:
                      writer.DirectContent.AddTemplate(page, 0, 1f, -1f, 0, pageWidth, 0);
                      break;

                    default:
                      throw new InvalidOperationException(string.Format("Unexpected page rotation: [{0}].", pageRotation));
                  }
                }
                var bookmarks = SimpleBookmark.GetBookmark(reader);
                writer.Outlines = bookmarks;
                EnsureIccProfile(writer);
                doc.Close();
                writer.Close();
              }
            }
            reader.Close();
          }
        }

        outStream.Seek(0, SeekOrigin.Begin);
        return new PdfAConversionResult { Content = outStream.ReadAllBytes(), IsPdfA = false, PageCount = pageCount };
      }
    }

    private Dictionary<string, object> CreateSimpleBookmark(string documentEntryTitle, int startPage, IDictionary<string, object> bookmarkStyles)
    {
      var bookmark = new Dictionary<string, object>();
      bookmark.Add("Title", documentEntryTitle);
      bookmark.Add("Color", "0.0 0.0 0.0");
      bookmark.Add("Open", "true");
      bookmark.Add("Action", "GoTo");
      bookmark.Add("Page", startPage + " /XYZ");
      if (bookmarkStyles != null)
      {
        foreach (var bookmarkStyle in bookmarkStyles)
        {
          if (bookmarkStyle.Value != null)
            bookmark[bookmarkStyle.Key] = bookmarkStyle.Value;
          else if (bookmark.ContainsKey(bookmarkStyle.Key))
            bookmark.Remove(bookmarkStyle.Key);
        }
      }
      return bookmark;
    }

    private PdfInfo GetPdfInfoInternal(byte[] pdfFile)
    {
      var pdfInfo = new PdfInfo();
      switch (GetPdfAConformanceLevel())
      {
        case PdfAConformanceLevel.PDF_A_1B:
          pdfInfo.ConfiguredConformanceLevel = PdfInfo.PdfConformanceEnum.PDF_A_1B;
          break;
        case PdfAConformanceLevel.PDF_A_2B:
          pdfInfo.ConfiguredConformanceLevel = PdfInfo.PdfConformanceEnum.PDF_A_2B;
          break;
        default:
          pdfInfo.ConfiguredConformanceLevel = null;
          break;
      }

      using (var stream = new MemoryStream(pdfFile))
      {
        using (var reader = new PdfReader(stream))
        {
          pdfInfo.PageCount = reader.NumberOfPages;
          var versionChar = reader.PdfVersion;
          switch (versionChar)
          {
            case '1':
              pdfInfo.Version = new System.Version(1, 1);
              break;
            case '2':
              pdfInfo.Version = new System.Version(1, 2);
              break;
            case '3':
              pdfInfo.Version = new System.Version(1, 3);
              break;
            case '4':
              pdfInfo.Version = new System.Version(1, 4);
              break;
            case '5':
              pdfInfo.Version = new System.Version(1, 5);
              break;
            case '6':
              pdfInfo.Version = new System.Version(1, 6);
              break;
            case '7':
              pdfInfo.Version = new System.Version(1, 7);
              break;
          }

          try
          {
            var xmpMeta = XmpMetaParser.Parse(reader.Metadata, null);
            var conformance = xmpMeta.GetProperty("http://www.aiim.org/pdfa/ns/id/", "pdfaid:conformance");
            var part = xmpMeta.GetProperty("http://www.aiim.org/pdfa/ns/id/", "pdfaid:part");
            if (conformance != null && part != null)
            {
              if (conformance.Value == "A")
              {
                switch (part.Value)
                {
                  case "1":
                    pdfInfo.ConformanceLevel = PdfInfo.PdfConformanceEnum.PDF_A_1A;
                    break;
                  case "2":
                    pdfInfo.ConformanceLevel = PdfInfo.PdfConformanceEnum.PDF_A_2A;
                    break;
                  case "3":
                    pdfInfo.ConformanceLevel = PdfInfo.PdfConformanceEnum.PDF_A_3A;
                    break;
                }
              }
              else if (conformance.Value == "B")
              {
                switch (part.Value)
                {
                  case "1":
                    pdfInfo.ConformanceLevel = PdfInfo.PdfConformanceEnum.PDF_A_1B;
                    break;
                  case "2":
                    pdfInfo.ConformanceLevel = PdfInfo.PdfConformanceEnum.PDF_A_2B;
                    break;
                  case "3":
                    pdfInfo.ConformanceLevel = PdfInfo.PdfConformanceEnum.PDF_A_3B;
                    break;
                }
              }
            }
          }
          catch
          {
            // ignored
          }
        }
      }

      return pdfInfo;
    }

    private static List<SourceDocumentInfo> GetSourceDocumentInfos(SourceDocument[] sourceFiles, Func<byte[], byte[]> modifyContents)
    {
      var sourceDocumentInfos = new List<SourceDocumentInfo>();
      foreach (var sourceDocument in sourceFiles.Where(p => p != null && p.Content != null && p.Content.Length > 0))
      {
        var sourceFilePath = Path.GetTempFileName();
        File.WriteAllBytes(sourceFilePath, modifyContents(sourceDocument.Content));
        sourceDocumentInfos.Add(
          new SourceDocumentInfo
          {
            Name = sourceDocument.Title,
            TempFileName = sourceFilePath,
            OutlineHierarchyMode = sourceDocument.HierarchyMode,
            StartOnOddPage = sourceDocument.StartOnOddPage,
            BookmarkStyles = sourceDocument.BookmarkStyles
          });
      }

      return sourceDocumentInfos;
    }

    private bool MergeBookmarksIfSameTitle(Dictionary<string, object> lastBookmark, Dictionary<string, object> currentBookmark)
    {
      if (lastBookmark == null || currentBookmark == null)
        return false;

      var lastTitle = (string)lastBookmark["Title"];
      var currentTitle = (string)currentBookmark["Title"];
      if (string.Equals(lastTitle, currentTitle))
      {
        object lastBookmarkKids;
        lastBookmark.TryGetValue(c_kidsBookmarks, out lastBookmarkKids);
        object currentBookmarkKids;
        lastBookmark.TryGetValue(c_kidsBookmarks, out currentBookmarkKids);

        if (currentBookmarkKids == null)
          return true;

        if (lastBookmarkKids == null)
        {
          lastBookmark.Add(c_kidsBookmarks, currentBookmarkKids);
          currentBookmark.Remove(c_kidsBookmarks);
        }
        else
          ((List<Dictionary<string, object>>)lastBookmarkKids).AddRange((List<Dictionary<string, object>>)currentBookmarkKids);

        return true;
      }
      return false;
    }

    private int MergeTo(List<SourceDocumentInfo> sourceStreams, Stream destination)
    {
      CheckNotNull(destination, "destination");
      CheckNotNullOrEmpty(sourceStreams, "sourceStreams");

      if (sourceStreams.Count == 1)
      {
        var sourceStream = sourceStreams.First().InputStream;
        sourceStream.Position = 0;
        sourceStream.CopyTo(destination);
        sourceStream.Position = 0;

        return GetNumberOfPagesInternal(sourceStream);
      }

      var tempFilePath = Path.GetTempFileName();

      try
      {
        var mergedDocuments = MergeToInternal(sourceStreams, tempFilePath, out var totalPageCount);
        MergeBookmarks(tempFilePath, destination, mergedDocuments);
        return totalPageCount;
      }
      finally
      {
        try
        {
          if (File.Exists(tempFilePath))
            File.Delete(tempFilePath);
        }
        catch (IOException)
        {
        }
      }
    }

    private void MergeBookmarks(string tempFilePath, Stream destination, List<MergedDocumentInfo> mergedDocuments)
    {
      using (var fileStream = File.Open(tempFilePath, FileMode.Open))
      {
        var reader = new PdfReader(fileStream);
        reader.MakeRemoteNamedDestinationsLocal();
        var stamper = new PdfStamper(reader, destination);

        var outlines = new List<Dictionary<string, object>>();
        Dictionary<string, object> lastBookmark = null;
        foreach (var mergedDocument in mergedDocuments)
        {
          Dictionary<string, object> currentBookmark;
          List<Dictionary<string, object>> subBookmarkCollection;
          switch (mergedDocument.OutlineHierarchyMode)
          {
            case SourceDocument.OutlineHierarchyMode.WholeHierarchy:
              currentBookmark = CreateSimpleBookmark(mergedDocument.Name, mergedDocument.StartPage, mergedDocument.BookmarkStyles);
              subBookmarkCollection = AddKidsCollection(currentBookmark);
              break;
            case SourceDocument.OutlineHierarchyMode.DescendantsOnly:
              currentBookmark = null;
              subBookmarkCollection = outlines;
              break;
            case SourceDocument.OutlineHierarchyMode.ThisOnly:
              currentBookmark = CreateSimpleBookmark(mergedDocument.Name, mergedDocument.StartPage, mergedDocument.BookmarkStyles);
              subBookmarkCollection = null;
              break;
            case SourceDocument.OutlineHierarchyMode.None:
              currentBookmark = null;
              subBookmarkCollection = null;
              break;
            default:
              throw new InvalidOperationException(
                $"Cannot handle outline hierarchy mode '{mergedDocument.OutlineHierarchyMode}'.");
          }

          if (subBookmarkCollection != null && mergedDocument.Bookmarks != null)
            subBookmarkCollection.AddRange(mergedDocument.Bookmarks);

          var isCurrentBookmarkMerged = MergeBookmarksIfSameTitle(lastBookmark, currentBookmark);
          if (isCurrentBookmarkMerged)
          {
            // ReSharper disable PossibleNullReferenceException
            if (currentBookmark.ContainsKey(c_kidsBookmarks))
              lastBookmark[c_kidsBookmarks] = currentBookmark[c_kidsBookmarks];
            // ReSharper restore PossibleNullReferenceException
            currentBookmark = null;
          }
          if (currentBookmark != null)
          {
            outlines.Add(currentBookmark);
            lastBookmark = currentBookmark;
          }
        }
        stamper.Outlines = outlines;
        stamper.Close();
      }
    }

    private static List<MergedDocumentInfo> MergeToInternal(List<SourceDocumentInfo> sourceStreams, string tempFilePath, out int totalPageCount)
    {
      totalPageCount = 0;
      var mergedDocuments = new List<MergedDocumentInfo>();
      using (var fileStream = File.Open(tempFilePath, FileMode.OpenOrCreate))
      {
        using (var document = new Document())
        {
          var pdfSmartCopy = new PdfSmartCopy(document, fileStream);
          var pageOffset = 0;
          document.Open();

          Rectangle lastDocumentsLastPageSize = null;
          var lastDocumentsLastPageRotation = 0;
          foreach (var sourceDocumentInfo in sourceStreams)
          {
            var reader = new PdfReader(sourceDocumentInfo.InputStream);

            try
            {
              reader.ConsolidateNamedDestinations();

              AssertNotEncrypted(reader);

              var numberOfPages = reader.NumberOfPages;
              var bookmarks = SimpleBookmark.GetBookmark(reader);

              var addBlankPage = sourceDocumentInfo.StartOnOddPage && numberOfPages > 0 && totalPageCount % 2 == 1;

              var startPageAt = addBlankPage ? pageOffset + 2 : pageOffset + 1;
              mergedDocuments.Add(
                new MergedDocumentInfo
                {
                  Name = sourceDocumentInfo.Name,
                  StartPage = startPageAt,
                  Bookmarks = bookmarks,
                  OutlineHierarchyMode = sourceDocumentInfo.OutlineHierarchyMode,
                  BookmarkStyles = sourceDocumentInfo.BookmarkStyles
                });

              if (addBlankPage && lastDocumentsLastPageSize != null)
              {
                pageOffset++;
                totalPageCount++;
                pdfSmartCopy.AddPage(lastDocumentsLastPageSize, lastDocumentsLastPageRotation);
              }

              lastDocumentsLastPageSize = reader.GetPageSize(numberOfPages);
              lastDocumentsLastPageRotation = reader.GetPageRotation(numberOfPages);

              if (bookmarks != null)
                SimpleBookmark.ShiftPageNumbers(bookmarks, pageOffset, null);

              for (var page = 0; page < numberOfPages;)
              {
                var pdfImportedPage = pdfSmartCopy.GetImportedPage(reader, ++page);

                pdfSmartCopy.AddPage(pdfImportedPage);
                totalPageCount++;
              }

              var namedDestinations = SimpleNamedDestination.GetNamedDestination(reader, false);
              pdfSmartCopy.AddNamedDestinations(namedDestinations, pageOffset);

              pageOffset += numberOfPages;
            }
            finally
            {
              reader.Close();
            }
          }

          document.Close();

          if (totalPageCount == 0)
            throw new InvalidOperationException("Cannot merge empty contents.");
        }
      }

      return mergedDocuments;
    }

    private static void AssertNotEncrypted(PdfReader reader)
    {
      if (reader.IsEncrypted())
        throw new PdfEncryptedException("Cannot process encrypted PDF.");
    }

    private byte[] ReadAllBytes(Stream inputStream)
    {
      if (inputStream is MemoryStream stream)
        return stream.ToArray();

      if (inputStream is FileStream fileStream)
        return fileStream.ReadAllBytes();

      using (var memoryStream = new MemoryStream())
      {
        inputStream.CopyTo(memoryStream);
        return memoryStream.ToArray();
      }
    }

    private byte[] ResizeInternal(byte[] source, Contract.PageSize pageSize, bool isLandscape, int margin)
    {
      CheckNotNullOrEmpty(source, "source");

      var sourceTempFile = Path.GetTempFileName();
      var destinationTempFile = Path.GetTempFileName();
      try
      {
        File.WriteAllBytes(sourceTempFile, source);

        using (var destinationStream = File.Open(destinationTempFile, FileMode.OpenOrCreate, FileAccess.ReadWrite))
        {
          var effectivePageSize = PageSizeConverter.Convert(pageSize, isLandscape);

          CreatePdf(
            destinationStream,
            effectivePageSize,
            margin,
            (doc, writer) => ResizeDocument(doc, writer, isLandscape, sourceTempFile));
        }
        return File.ReadAllBytes(destinationTempFile);
      }
      finally
      {
        DeleteFile(destinationTempFile);
        DeleteFile(sourceTempFile);
      }
    }

    private void ResizeDocument(Document doc, PdfWriter writer, bool isLandscape, string sourceTempFile)
    {
      using (var sourceStream = File.Open(sourceTempFile, FileMode.Open, FileAccess.Read))
      {
        using (var reader = new PdfReader(sourceStream))
        {
          for (var i = 1; i <= reader.NumberOfPages; i++)
          {
            doc.NewPage();
            var page = writer.GetImportedPage(reader, i);
            var image = Image.GetInstance(page);

            if (IsLandscape(reader.GetPageSizeWithRotation(i)) && !isLandscape)
              image.RotationDegrees = -90;
            else if (!IsLandscape(reader.GetPageSizeWithRotation(i)) && isLandscape)
              image.RotationDegrees = 90;

            image.ScaleToFit(
              doc.PageSize.Width - doc.LeftMargin - doc.RightMargin,
              doc.PageSize.Height - doc.TopMargin - doc.BottomMargin);
            doc.Add(image);
          }

          doc.Close();
        }
      }
    }

    private class SourceDocumentInfo
    {
      public IDictionary<string, object> BookmarkStyles { get; set; }
      public Stream InputStream { get; set; }
      public string Name { get; set; }
      public SourceDocument.OutlineHierarchyMode OutlineHierarchyMode { get; set; }
      public bool StartOnOddPage { get; set; }
      public string TempFileName { get; set; }
    }

    private class MergedDocumentInfo
    {
      public IList<Dictionary<string, object>> Bookmarks { get; set; }
      public IDictionary<string, object> BookmarkStyles { get; set; }
      public string Name { get; set; }
      public SourceDocument.OutlineHierarchyMode OutlineHierarchyMode { get; set; }
      public int StartPage { get; set; }
    }

    private class PageSizeConverter
    {
      public static Rectangle Convert(Contract.PageSize pageSize, bool isLandscape)
      {
        Rectangle rectangle;
        switch (pageSize)
        {
          case Contract.PageSize.A0:
            rectangle = iTextSharp.text.PageSize.A0;
            break;
          case Contract.PageSize.A1:
            rectangle = iTextSharp.text.PageSize.A1;
            break;
          case Contract.PageSize.A2:
            rectangle = iTextSharp.text.PageSize.A2;
            break;
          case Contract.PageSize.A3:
            rectangle = iTextSharp.text.PageSize.A3;
            break;
          case Contract.PageSize.A4:
            rectangle = iTextSharp.text.PageSize.A4;
            break;
          case Contract.PageSize.A5:
            rectangle = iTextSharp.text.PageSize.A5;
            break;
          case Contract.PageSize.A6:
            rectangle = iTextSharp.text.PageSize.A6;
            break;
          case Contract.PageSize.A7:
            rectangle = iTextSharp.text.PageSize.A7;
            break;
          case Contract.PageSize.A8:
            rectangle = iTextSharp.text.PageSize.A8;
            break;
          case Contract.PageSize.A9:
            rectangle = iTextSharp.text.PageSize.A9;
            break;
          case Contract.PageSize.A10:
            rectangle = iTextSharp.text.PageSize.A10;
            break;
          case Contract.PageSize.B0:
            rectangle = iTextSharp.text.PageSize.B0;
            break;
          case Contract.PageSize.B1:
            rectangle = iTextSharp.text.PageSize.B1;
            break;
          case Contract.PageSize.B2:
            rectangle = iTextSharp.text.PageSize.B2;
            break;
          case Contract.PageSize.B3:
            rectangle = iTextSharp.text.PageSize.B3;
            break;
          case Contract.PageSize.B4:
            rectangle = iTextSharp.text.PageSize.B4;
            break;
          case Contract.PageSize.B5:
            rectangle = iTextSharp.text.PageSize.B5;
            break;
          case Contract.PageSize.B6:
            rectangle = iTextSharp.text.PageSize.B6;
            break;
          case Contract.PageSize.B7:
            rectangle = iTextSharp.text.PageSize.B7;
            break;
          case Contract.PageSize.B8:
            rectangle = iTextSharp.text.PageSize.B8;
            break;
          case Contract.PageSize.B9:
            rectangle = iTextSharp.text.PageSize.B9;
            break;
          case Contract.PageSize.B10:
            rectangle = iTextSharp.text.PageSize.B10;
            break;
          case Contract.PageSize.LETTER:
            rectangle = iTextSharp.text.PageSize.LETTER;
            break;
          case Contract.PageSize.LEGAL:
            rectangle = iTextSharp.text.PageSize.LEGAL;
            break;
          case Contract.PageSize.POSTCARD:
            rectangle = iTextSharp.text.PageSize.POSTCARD;
            break;
          default:
            rectangle = null;
            break;
        }

        if (rectangle != null && isLandscape)
          rectangle.Rotate();
        return rectangle;
      }
    }
  }
}