﻿// Copyright (c) RUBICON IT GmbH
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Rubicon.PdfService.Contract
{
  [Serializable]
  public class PdfEncryptedException : Exception
  {
    public PdfEncryptedException()
    {
    }

    public PdfEncryptedException(string message) : base(message)
    {
    }

    public PdfEncryptedException(string message, Exception inner) : base(message, inner)
    {
    }

    protected PdfEncryptedException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
  }
}
