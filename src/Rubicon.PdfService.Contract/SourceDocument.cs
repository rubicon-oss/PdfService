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

using System.Collections.Generic;
using System.Xml.Serialization;

namespace Rubicon.PdfService.Contract
{
  [XmlRoot(Namespace = "http://www.rubicon.eu/Rubicon.Pdf.Service/v1/SourceDocument")]
  public class SourceDocument
  {
    public enum OutlineHierarchyMode
    {
      None = 0,
      DescendantsOnly = 1,
      ThisOnly = 2,
      WholeHierarchy = 3
    }

    public string Title;
    public byte[] Content;
    public OutlineHierarchyMode HierarchyMode;

    public IDictionary<string, object> BookmarkStyles;

    /// <summary>
    /// Determines if a blank pages is added before the next Pdf is merged
    /// </summary>
    public bool StartOnOddPage;
  }
}