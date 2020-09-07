// Copyright (c) RUBICON IT GmbH
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
// 
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, see <http://www.gnu.org/licenses/>.

using System.ServiceModel;

namespace Rubicon.PdfService.Contract
{
  [ServiceContract(Namespace = "http://www.rubicon.eu/Rubicon.Pdf.Service/v1/")]
  public interface IPdfService
  {
    /// <summary>
    /// Converts an image to Pdf
    /// </summary>
    /// <param name="imageData">Image byte array</param>
    /// <param name="pageSize">Destination page size of Pdf</param>
    /// <param name="margins">Margins around the images</param>
    /// <returns>Returns a byte array</returns>
    [OperationContract]
    byte[] ConvertImage(byte[] imageData, PageSize pageSize, int? margins);

    /// <summary>
    /// Merges all given Pdf files to one Pdf
    /// </summary>
    /// <param name="pdfFiles">all Pdf files to merge</param>
    /// <returns>Returns the Result <see cref="Result"/></returns>
    [OperationContract]
    Result Merge(SourceDocument[] pdfFiles);

    /// <summary>
    /// Merges all given Pdf files to one Pdf and changes page size and orientation
    /// </summary>
    /// <param name="pdfFiles">all Pdf files to merge</param>
    /// <param name="pageSize">The page size for the destination Pdf</param>
    /// <param name="isLandscape">The orientation of the destination Pdf</param>
    /// <returns>Returns the Result <see cref="Result"/></returns>
    [OperationContract]
    Result ResizeMerge(SourceDocument[] pdfFiles, PageSize pageSize, bool isLandscape);

    /// <summary>
    /// Resizes a Pdf
    /// </summary>
    /// <param name="pdfFile">Byte array of Pdf file to resize</param>
    /// <param name="pageSize">The page size for the destination Pdf</param>
    /// <param name="isLandscape">The orientation of the destination Pdf</param>
    /// <param name="margin">Margin</param>
    /// <returns>Returns a byte array</returns>
    [OperationContract]
    byte[] Resize(byte[] pdfFile, PageSize pageSize, bool isLandscape, int margin);

    /// <summary>
    /// Generates a new Pdf with a centered text
    /// </summary>
    /// <param name="lines">The text to be placed</param>
    /// <param name="fontName">The font name</param>
    /// <param name="fontSize">The font size</param>
    /// <param name="pageSize">The page size</param>
    /// <param name="isLandscape">The orientation</param>
    /// <returns>Returns a byte array</returns>
    [OperationContract]
    byte[] CreateNewPdfWithCenteredText(string[] lines, string fontName, float fontSize, PageSize pageSize, bool isLandscape);

    /// <summary>
    /// Determines the total number of pages of a Pdf
    /// </summary>
    /// <param name="pdfFile">The byte array of the Pdf file</param>
    /// <returns>Returns a byte array</returns>
    [OperationContract]
    int GetNumberOfPages(byte[] pdfFile);

    /// <summary>
    /// Adds a white rectangle and a black page number to each page at the bottom center.
    /// Pages are numbered consecutively, starting at <paramref name="firstPageNumberToUse"/>
    /// </summary>
    /// <param name="pdfFile">The PDF document whose pages to number.</param>
    /// <param name="pagesToSkipBeforeNumbering">When greater than zero, the given number of pages at the start of the document 
    /// will not receive page numbers.</param>
    /// <param name="firstPageNumberToUse">The first page number that will be used.</param>
    /// <param name="totalPageCount">If a value is provided, it is used to add page numbers together with a page count, like '17 / 35'.
    /// <note>Provide the total pages in the document, not the number of pages with page numbers.
    /// This is independent of <paramref name="pagesToSkipBeforeNumbering"/>, i.e. even when skipping pages, the first page receiving a number
    /// will be numbered with the given number.</note></param>
    /// <param name="fontName">The font name</param>
    /// <param name="fontSize">The font size</param>
    /// <param name="margin">Margin</param>
    /// <returns>The original PDF document with added page numbers.</returns>
    [OperationContract]
    byte[] AddPageNumbers(byte[] pdfFile, int pagesToSkipBeforeNumbering, int firstPageNumberToUse, int? totalPageCount, string fontName, float fontSize, float margin);

    /// <summary>
    /// Adds the page-specific text provided by <paramref name="pageOverlayText"/> to each page in <paramref name="pdfFile"/>,
    /// positioning it accoring to the <paramref name="verticalPlacement">vertical</paramref> and 
    /// <paramref name="horizontalPlacement">horizontal</paramref> placement indicators.
    /// <note>An overlay is a white box with black text in it.</note>
    /// <note>The box is fit to accomodate the whole text.</note> 
    /// <note>Line breaks are not supported.</note>
    /// </summary>
    /// <param name="pdfFile">A PDF document whose pages will receive overlays.</param>
    /// <param name="pageOverlayText">A function returning a text for the page with the given (zero-based) index.</param>
    /// <param name="verticalPlacement">Where to put the overlay on the vertical axis.</param>
    /// <param name="horizontalPlacement">Where to put the overlay on the horizontal axis.</param>
    /// <param name="fontName">The font name</param>
    /// <param name="fontSize">The font size</param>
    /// <param name="margin">Margin</param>
    /// <returns>The original <paramref name="pdfFile"/> with the added overlays.</returns>
    [OperationContract]
    byte[] AddOverlay(byte[] pdfFile, string pageOverlayText, OverlayPlacementVertical verticalPlacement, OverlayPlacementHorizontal horizontalPlacement, string fontName, float fontSize, float margin);


    /// <summary>
    /// Converts a Pdf file to a Pdf/A-1b
    /// </summary>
    /// <param name="pdfFile">Image byte array</param>
    /// <returns>Returns a PdfAConversionResult</returns>
    [OperationContract]
    PdfAConversionResult ConvertToPdfA(byte[] pdfFile);

    [OperationContract]
    PdfInfo GetPdfInfo(byte[] pdfFile);
  }
}
