using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace Rubicon.PdfService.Contract;

[XmlRoot]
public class SourceDocumentInfo
{
  public string FilePath;
  public string Title;
  public SourceDocument.OutlineHierarchyMode HierarchyMode;
  public bool StartOnOddPage;
  
  [XmlIgnore] 
  public IDictionary<string, object> BookmarkStyles;

  [XmlArray("BookmarkStyles")]
  public SerializableKeyValuePair<string, object>[] BookmarkStylesSerializationHelper
  {
    get => BookmarkStyles?.Select(p => p.ToSerializablePair()).ToArray();
    set => BookmarkStyles = value?.ToDictionary(p => p.Key, p => p.Value);
  }
}