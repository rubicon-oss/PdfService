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

using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Rubicon.PdfService.Contract;

namespace Rubicon.PdfService;

public static class Program
{
  private const int c_success = 0;
  private const int c_timeout = 10;
  private const int c_inputFileError = 20;
  private const int c_outputFileError = 21;
  private const int c_processingError = 22;
  private const int c_invalidArguments = 23;

  private static int s_returnCode = c_success;

  // Global options
  private static readonly Option<Mode> s_modeOption = new(["--mode", "-m"], "Resulting PDF standard") { IsRequired = true };
  private static readonly Option<int> s_timeoutOption = new(["--timeout", "-t"], "Timeout for operation in milliseconds") { IsRequired = true };
  private static readonly Option<FileInfo> s_outputOption = new(["--output", "-o"], "Output file") { IsRequired = true };
  private static readonly Option<string> s_iccProfilePathOption = new(["--iccprofilepath", "-pp"], "ICC profile path");
  private static readonly Option<string> s_iccProfileNameOption = new(["--iccprofilename", "-pn"], "ICC profile name");
  private static readonly Option<PdfAVersion?> s_pdfAVersionOption = new(["--pdfaversion", "-pv"], "PDF/A version to use");

  // Command specific options
  private static readonly Option<FileInfo> s_inputOption = new(["--input", "-i"], "Input file") { IsRequired = true };
  private static readonly Option<PageSize> s_pageSizeOption = new(["--pagesize", "-p"], "Page size of output file") { IsRequired = true };
  private static readonly Option<int?> s_marginOption = new(["--margin", "-ma"], "Margin around the image");
  private static readonly Option<FileInfo[]> s_multipleInputsOption = new(["--input", "-i"], "Multiple input files in SourceDocument format.") { IsRequired = true };
  private static readonly Option<Orientation> s_orientationOption = new(["--orientation", "-or"], "Page orientation for the resulting PDF file.") { IsRequired = true };
  private static readonly Option<string> s_fontNameOption = new(["--font", "-f"], "Font name.") { IsRequired = true };
  private static readonly Option<float> s_fontSizeOption = new(["--fontsize", "-fs"], "Font size.") { IsRequired = true };
  private static readonly Option<int> s_pagesToSkipBeforeNumberingOption = new(["--skip", "-s"], "The number of pages that should be skipped before numbering should start.") { IsRequired = true };
  private static readonly Option<int> s_firstPageNumberToUseOption = new(["--firstnumber", "-fn"], "The first page number that will be used.") { IsRequired = true };
  private static readonly Option<int?> s_totalPageCountOption = new(["--totalpages", "-tp"], "If a value is provided, it is used to add page numbers together with a page count, like '17 / 35'.") { IsRequired = true };
  private static readonly Option<FileInfo> s_pageOverlayTextOption = new(["--overlaytext", "-ot"], "The file that contains the text for the overlay") { IsRequired = true };
  private static readonly Option<OverlayPlacementVertical> s_verticalPlacementOption = new(["--vertical", "-v"], "The vertical position the overlay should be placed in.") { IsRequired = true };
  private static readonly Option<OverlayPlacementHorizontal> s_horizontalPlacementOption = new(["--horizontal", "-h"], "The horizontal position the overlay should be placed in.") { IsRequired = true };

  public static int Main(string[] args)
  {
    var rootCommand = new RootCommand("Convert into PDF or PDF/A");

    rootCommand.AddGlobalOption(s_modeOption);
    rootCommand.AddGlobalOption(s_timeoutOption);
    rootCommand.AddGlobalOption(s_iccProfilePathOption);
    rootCommand.AddGlobalOption(s_iccProfileNameOption);
    rootCommand.AddGlobalOption(s_pdfAVersionOption);

    var convertImageCommand = new Command("ConvertImage", "Convert an image to a PDF file.");
    convertImageCommand.AddOption(s_pageSizeOption);
    convertImageCommand.AddOption(s_marginOption);
    convertImageCommand.AddOption(s_inputOption);
    convertImageCommand.AddOption(s_outputOption);
    convertImageCommand.SetHandler(HandlerForConvertImage);
    rootCommand.Add(convertImageCommand);

    var mergeCommand = new Command("Merge", "Merge multiple PDF files into a single PDF file.");
    mergeCommand.AddOption(s_multipleInputsOption);
    mergeCommand.AddOption(s_outputOption);
    mergeCommand.SetHandler(HandlerForMerge);
    rootCommand.Add(mergeCommand);

    var resizeMergeCommand = new Command("ResizeMerge", "Merge multiple PDF files into a single PDF file and resize them.");
    resizeMergeCommand.AddOption(s_multipleInputsOption);
    resizeMergeCommand.AddOption(s_pageSizeOption);
    resizeMergeCommand.AddOption(s_orientationOption);
    resizeMergeCommand.AddOption(s_outputOption);
    resizeMergeCommand.SetHandler(HandlerForResizeMerge);
    rootCommand.Add(resizeMergeCommand);

    var resizeCommand = new Command("Resize", "Resize PDF.");
    resizeCommand.AddOption(s_inputOption);
    resizeCommand.AddOption(s_pageSizeOption);
    resizeCommand.AddOption(s_orientationOption);
    resizeCommand.AddOption(s_marginOption);
    resizeCommand.AddOption(s_outputOption);
    resizeCommand.SetHandler(HandlerForResize);
    rootCommand.Add(resizeCommand);

    var createNewPdfWithCenteredTextCommand = new Command("CreateNewPdfWithCenteredText", "Create a new PDF file with centered text from an txt file.");
    createNewPdfWithCenteredTextCommand.AddOption(s_inputOption);
    createNewPdfWithCenteredTextCommand.AddOption(s_fontNameOption);
    createNewPdfWithCenteredTextCommand.AddOption(s_fontSizeOption);
    createNewPdfWithCenteredTextCommand.AddOption(s_pageSizeOption);
    createNewPdfWithCenteredTextCommand.AddOption(s_orientationOption);
    createNewPdfWithCenteredTextCommand.AddOption(s_outputOption);
    createNewPdfWithCenteredTextCommand.SetHandler(HandlerForCreateNewPdfWithCenteredText);
    rootCommand.Add(createNewPdfWithCenteredTextCommand);

    var getNumberOfPagesCommand = new Command("GetNumberOfPages", "Get the number of pages in the given PDF file.");
    getNumberOfPagesCommand.AddOption(s_inputOption);
    getNumberOfPagesCommand.SetHandler(HandlerForGetNumberOfPages);
    rootCommand.Add(getNumberOfPagesCommand);

    var addPageNumbersCommand = new Command("AddPageNumbers", "Add page numbers to existing PDF file.");
    addPageNumbersCommand.AddOption(s_inputOption);
    addPageNumbersCommand.AddOption(s_outputOption);
    addPageNumbersCommand.AddOption(s_pagesToSkipBeforeNumberingOption);
    addPageNumbersCommand.AddOption(s_firstPageNumberToUseOption);
    addPageNumbersCommand.AddOption(s_totalPageCountOption);
    addPageNumbersCommand.AddOption(s_fontNameOption);
    addPageNumbersCommand.AddOption(s_fontSizeOption);
    addPageNumbersCommand.AddOption(s_marginOption);
    addPageNumbersCommand.SetHandler(HandlerForAddPageNumbers);
    rootCommand.Add(addPageNumbersCommand);

    var addOverlayCommand = new Command("AddOverlay", "Add an overlay to every page in the given PDF file.");
    addOverlayCommand.AddOption(s_inputOption);
    addOverlayCommand.AddOption(s_outputOption);
    addOverlayCommand.AddOption(s_pageOverlayTextOption);
    addOverlayCommand.AddOption(s_verticalPlacementOption);
    addOverlayCommand.AddOption(s_horizontalPlacementOption);
    addOverlayCommand.AddOption(s_fontNameOption);
    addOverlayCommand.AddOption(s_fontSizeOption);
    addOverlayCommand.AddOption(s_marginOption);
    addOverlayCommand.SetHandler(HandlerForAddOverlay);
    rootCommand.Add(addOverlayCommand);

    var convertToPdfACommand = new Command("ConvertToPdfA", "Convert PDF to PDF/A.");
    convertToPdfACommand.AddOption(s_inputOption);
    convertToPdfACommand.AddOption(s_outputOption);
    convertToPdfACommand.SetHandler(HandlerForConvertToPdfA);
    rootCommand.Add(convertToPdfACommand);

    var getPdfInfoCommand = new Command("GetPdfInfo", "Get information about the PDF file.");
    getPdfInfoCommand.AddOption(s_inputOption);
    getPdfInfoCommand.AddOption(s_outputOption);
    getPdfInfoCommand.SetHandler(HandlerForGetPdfInfo);
    rootCommand.Add(getPdfInfoCommand);

    var commandLineBuilder = new CommandLineBuilder(rootCommand)
      .UseDefaults()
      .UseHelp(context =>
      {
        context.HelpBuilder.CustomizeLayout(_ =>
          HelpBuilder.Default
            .GetLayout()
            .Append(_ => _.Output.WriteLine($"""

                                             Return codes:
                                               {c_success,3}: Success
                                               {c_timeout,3}: Timeout
                                               {c_inputFileError,3}: Error while processing input file(s)
                                               {c_outputFileError,3}: Error while writing output file
                                               {c_processingError,3}: Error while processing data
                                               {c_invalidArguments,3}: Invalid or missing command line arguments
                                             """)));
      })
      .Build();

    var result = commandLineBuilder.Invoke(args);
    if (s_returnCode == c_success && result != 0)
      s_returnCode = c_invalidArguments;

    return s_returnCode;
  }

  private static void HandlerForGetPdfInfo(InvocationContext context)
  {
    ProcessWithTimeout(context, ctx =>
    {
      var inputFileContent = ReadFile(ctx);

      PdfInfo result;
      try
      {
        var pdfService = GetService(ctx);
        result = pdfService.GetPdfInfo(inputFileContent);
      }
      catch (Exception ex)
      {
        s_returnCode = c_processingError;
        throw new InvalidOperationException("Unable to convert to PDF/A.", ex);
      }

      WriteXmlFile(ctx, result);
    });
  }

  private static void HandlerForConvertToPdfA(InvocationContext context)
  {
    ProcessWithTimeout(context, ctx =>
    {
      var inputFileContent = ReadFile(ctx);

      PdfAConversionResult result;
      try
      {
        var pdfService = GetService(ctx);
        result = pdfService.ConvertToPdfA(inputFileContent);
      }
      catch (Exception ex)
      {
        s_returnCode = c_processingError;
        throw new InvalidOperationException("Unable to convert to PDF/A.", ex);
      }

      WriteXmlFile(ctx, result);
    });
  }

  private static void HandlerForAddOverlay(InvocationContext context)
  {
    ProcessWithTimeout(context, ctx =>
    {
      var verticalPlacement = ctx.ParseResult.GetValueForOption(s_verticalPlacementOption);
      var horizontalPlacement = ctx.ParseResult.GetValueForOption(s_horizontalPlacementOption);
      var fontName = ctx.ParseResult.GetValueForOption(s_fontNameOption);
      var fontSize = ctx.ParseResult.GetValueForOption(s_fontSizeOption);
      var margin = ctx.ParseResult.GetValueForOption(s_marginOption) ?? 0f;

      var inputFileContent = ReadFile(ctx);
      var pageOverlayText = ReadOverlayTextFile(ctx);

      byte[] result;
      try
      {
        var pdfService = GetService(ctx);
        result = pdfService.AddOverlay(inputFileContent, pageOverlayText, verticalPlacement, horizontalPlacement, fontName, fontSize, margin);
      }
      catch (Exception ex)
      {
        s_returnCode = c_processingError;
        throw new InvalidOperationException("Unable to add overlay to PDF.", ex);
      }

      WriteFile(ctx, result);
    });
  }

  private static void HandlerForAddPageNumbers(InvocationContext context)
  {
    ProcessWithTimeout(context, ctx =>
    {
      var pagesToSkipBeforeNumbering = ctx.ParseResult.GetValueForOption(s_pagesToSkipBeforeNumberingOption);
      var firstPageNumberToUse = ctx.ParseResult.GetValueForOption(s_firstPageNumberToUseOption);
      var totalPageCount = ctx.ParseResult.GetValueForOption(s_totalPageCountOption);
      var fontName = ctx.ParseResult.GetValueForOption(s_fontNameOption);
      var fontSize = ctx.ParseResult.GetValueForOption(s_fontSizeOption);
      var margin = ctx.ParseResult.GetValueForOption(s_marginOption) ?? 0f;

      var inputFileContent = ReadFile(ctx);

      byte[] result;
      try
      {
        var pdfService = GetService(ctx);
        result = pdfService.AddPageNumbers(inputFileContent, pagesToSkipBeforeNumbering, firstPageNumberToUse, totalPageCount, fontName, fontSize, margin);
      }
      catch (Exception ex)
      {
        s_returnCode = c_processingError;
        throw new InvalidOperationException("Unable to add page numbers to PDF.", ex);
      }

      WriteFile(ctx, result);
    });
  }

  private static void HandlerForGetNumberOfPages(InvocationContext context)
  {
    ProcessWithTimeout(context, ctx =>
    {
      var inputFileContent = ReadFile(ctx);

      int result;
      try
      {
        var pdfService = GetService(ctx);
        result = pdfService.GetNumberOfPages(inputFileContent);
      }
      catch (Exception ex)
      {
        s_returnCode = c_processingError;
        throw new InvalidOperationException("Unable to determine number of pages in PDF.", ex);
      }

      Console.WriteLine(result);
    });
  }

  private static void HandlerForCreateNewPdfWithCenteredText(InvocationContext context)
  {
    ProcessWithTimeout(context, ctx =>
    {
      var pageSize = ctx.ParseResult.GetValueForOption(s_pageSizeOption);
      var fontName = ctx.ParseResult.GetValueForOption(s_fontNameOption);
      var fontSize = ctx.ParseResult.GetValueForOption(s_fontSizeOption);
      var orientation = ctx.ParseResult.GetValueForOption(s_orientationOption);

      var inputFileContent = ReadTextFile(ctx);

      byte[] result;
      try
      {
        var pdfService = GetService(ctx);
        result = pdfService.CreateNewPdfWithCenteredText(inputFileContent, fontName, fontSize, pageSize, orientation == Orientation.Landscape);
      }
      catch (Exception ex)
      {
        s_returnCode = c_processingError;
        throw new InvalidOperationException("Unable to create PDF.", ex);
      }

      WriteFile(ctx, result);
    });
  }

  private static void HandlerForResize(InvocationContext context)
  {
    ProcessWithTimeout(context, ctx =>
    {
      var pageSize = ctx.ParseResult.GetValueForOption(s_pageSizeOption);
      var margin = ctx.ParseResult.GetValueForOption(s_marginOption) ?? 0;
      var orientation = ctx.ParseResult.GetValueForOption(s_orientationOption);

      var inputFileContent = ReadFile(ctx);

      byte[] result;
      try
      {
        var pdfService = GetService(ctx);
        result = pdfService.Resize(inputFileContent, pageSize, orientation == Orientation.Landscape, margin);
      }
      catch (Exception ex)
      {
        s_returnCode = c_processingError;
        throw new InvalidOperationException("Unable to resize PDF.", ex);
      }

      WriteFile(ctx, result);
    });
  }

  private static void HandlerForResizeMerge(InvocationContext context)
  {
    ProcessWithTimeout(context, ctx =>
    {
      var multipleInputFiles = ctx.ParseResult.GetValueForOption(s_multipleInputsOption);
      var pageSize = ctx.ParseResult.GetValueForOption(s_pageSizeOption);
      var orientation = ctx.ParseResult.GetValueForOption(s_orientationOption);

      var inputContents = GetSourceDocuments(multipleInputFiles);

      Result result;
      try
      {
        var pdfService = GetService(ctx);
        result = pdfService.ResizeMerge(inputContents, pageSize, orientation == Orientation.Landscape);
      }
      catch (Exception ex)
      {
        s_returnCode = c_processingError;
        throw new InvalidOperationException("Unable to merge PDF files.", ex);
      }

      WriteFile(ctx, result.Content);
      Console.WriteLine(result.PageCount);
    });
  }

  private static void HandlerForMerge(InvocationContext context)
  {
    ProcessWithTimeout(context, ctx =>
    {
      var multipleInputFiles = ctx.ParseResult.GetValueForOption(s_multipleInputsOption);

      var inputContents = GetSourceDocuments(multipleInputFiles);

      Result result;
      try
      {
        var pdfService = GetService(ctx);
        result = pdfService.Merge(inputContents);
      }
      catch (Exception ex)
      {
        s_returnCode = c_processingError;
        throw new InvalidOperationException("Unable to merge PDF files.", ex);
      }

      WriteFile(ctx, result.Content);
      Console.WriteLine(result.PageCount);
    });
  }

  private static void HandlerForConvertImage(InvocationContext context)
  {
    ProcessWithTimeout(context, ctx =>
    {
      var pageSize = ctx.ParseResult.GetValueForOption(s_pageSizeOption);
      var margin = ctx.ParseResult.GetValueForOption(s_marginOption);

      var inputFileContent = ReadFile(ctx);

      byte[] result;
      try
      {
        var pdfService = GetService(ctx);
        result = pdfService.ConvertImage(inputFileContent, pageSize, margin);
      }
      catch (Exception ex)
      {
        s_returnCode = c_processingError;
        throw new InvalidOperationException("Unable to convert image to PDF.", ex);
      }

      WriteFile(ctx, result);
    });
  }

  private static void ProcessWithTimeout(InvocationContext context, Action<InvocationContext> action)
  {
    var timeout = context.ParseResult.GetValueForOption(s_timeoutOption);

    var task = Task.Run(() => { action(context); });

    if (!task.Wait(TimeSpan.FromMilliseconds(timeout)))
      s_returnCode = c_timeout;
  }

  private static IPdfService GetService(InvocationContext context)
  {
    var mode = context.ParseResult.GetValueForOption(s_modeOption);
    var iccProfilePath = context.ParseResult.GetValueForOption(s_iccProfilePathOption);
    var iccProfileName = context.ParseResult.GetValueForOption(s_iccProfileNameOption);
    var pdfAVersion = context.ParseResult.GetValueForOption(s_pdfAVersionOption);

    return mode switch
    {
      Mode.Pdf => new PdfService(iccProfilePath, iccProfileName, pdfAVersion),
      Mode.PdfA => new PdfAService(iccProfilePath, iccProfileName, pdfAVersion),
      _ => throw new InvalidOperationException($"Mode '{mode}' not available.")
    };
  }

  private static SourceDocument[] GetSourceDocuments(FileInfo[] multipleInputFiles)
  {
    var inputContents = new SourceDocument[multipleInputFiles.Length];
    var xmlSerializer = new XmlSerializer(typeof(SourceDocument));
    for (var i = 0; i < multipleInputFiles.Length; i++)
      using (var streamReader = File.OpenText(multipleInputFiles[i].FullName))
      {
        var sourceDocument = (SourceDocument)xmlSerializer.Deserialize(streamReader);
        inputContents[i] = sourceDocument;
      }

    return inputContents;
  }

  private static string[] ReadTextFile(InvocationContext context)
  {
    var inputFilename = context.ParseResult.GetValueForOption(s_inputOption);
    try
    {
      return File.ReadAllLines(inputFilename.FullName, Encoding.UTF8);
    }
    catch (Exception ex)
    {
      s_returnCode = c_inputFileError;
      throw new InvalidOperationException($"Unable to read file {inputFilename}.", ex);
    }
  }

  private static string ReadOverlayTextFile(InvocationContext context)
  {
    var inputFilename = context.ParseResult.GetValueForOption(s_pageOverlayTextOption);
    try
    {
      return File.ReadAllText(inputFilename.FullName, Encoding.UTF8);
    }
    catch (Exception ex)
    {
      s_returnCode = c_inputFileError;
      throw new InvalidOperationException($"Unable to read file {inputFilename}.", ex);
    }
  }

  private static byte[] ReadFile(InvocationContext context)
  {
    var inputFilename = context.ParseResult.GetValueForOption(s_inputOption);
    try
    {
      return File.ReadAllBytes(inputFilename.FullName);
    }
    catch (Exception ex)
    {
      s_returnCode = c_inputFileError;
      throw new InvalidOperationException($"Unable to read file {inputFilename}.", ex);
    }
  }

  private static void WriteFile(InvocationContext context, byte[] content)
  {
    var outputFilename = context.ParseResult.GetValueForOption(s_outputOption);
    try
    {
      if (outputFilename.Exists)
        throw new InvalidOperationException($"'{outputFilename}' already exists and will not be overwritten.");

      File.WriteAllBytes(outputFilename.FullName, content);
    }
    catch (Exception ex)
    {
      s_returnCode = c_outputFileError;
      throw new InvalidOperationException($"Unable to write file {outputFilename}.", ex);
    }
  }

  private static void WriteXmlFile<T>(InvocationContext context, T content)
  {
    var outputFilename = context.ParseResult.GetValueForOption(s_outputOption);
    try
    {
      var xmlSerializer = new XmlSerializer(typeof(T));
      using (var xmlWriter = XmlWriter.Create(outputFilename.FullName, new XmlWriterSettings { Encoding = Encoding.UTF8 }))
      {
        xmlSerializer.Serialize(xmlWriter, content);
      }
    }
    catch (Exception ex)
    {
      s_returnCode = c_outputFileError;
      throw new InvalidOperationException($"Unable to write file {outputFilename}.", ex);
    }
  }
}