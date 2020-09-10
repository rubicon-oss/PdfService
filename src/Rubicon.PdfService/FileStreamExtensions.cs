using System;
using System.IO;
using JetBrains.Annotations;

namespace Rubicon.PdfService
{
  public static class FileStreamExtensions
  {
    public static byte[] ReadAllBytes([NotNull] this FileStream fileStream)
    {
      if(fileStream == null)
        throw new ArgumentNullException(nameof(fileStream));

      int offset = 0;
      long length = fileStream.Length;
      if (length > (long)int.MaxValue)
        throw new IOException("File is too large");

      int count = (int)length;
      var buffer = new byte[count];
      int num;
      for (; count > 0; count -= num)
      {
        num = fileStream.Read(buffer, offset, count);
        if (num == 0)
          throw new EndOfStreamException();
        offset += num;
      }

      return buffer;
    }
  }
}
