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

using System.Xml.Serialization;

namespace Rubicon.PdfService.Contract
{
  [XmlRoot(Namespace = "https://www.rubicon.eu/Rubicon.Pdf.Service/v1/PdfAConversionResult")]
  public class PdfAConversionResult
  {
    public byte[] Content;
    public bool IsPdfA;
    public int PageCount;
  }
}