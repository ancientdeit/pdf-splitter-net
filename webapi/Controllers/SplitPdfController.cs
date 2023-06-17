using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ICSharpCode.SharpZipLib.Zip;

namespace PdfSplitter.Controllers
{
    [Route("api/[controller]")]
    public class UploadController : Controller
    {
        [HttpPost]
        public IActionResult Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            string inputFileName = Path.GetFileNameWithoutExtension(file.FileName);

            using (var stream = new MemoryStream())
            {
                file.CopyTo(stream);
                stream.Position = 0;

                using (var pdfReader = new PdfReader(stream))
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        using (var zipOutputStream = new ZipOutputStream(memoryStream))
                        {
                            for (int i = 1; i <= pdfReader.NumberOfPages; i++)
                            {
                                using (var pageStream = new MemoryStream())
                                {
                                    var document = new Document(pdfReader.GetPageSizeWithRotation(i));
                                    var pdfCopy = new PdfCopy(document, pageStream);
                                    document.Open();

                                    var page = pdfCopy.GetImportedPage(pdfReader, i);
                                    pdfCopy.AddPage(page);

                                    document.Close();

                                    // Compress the page before writing it to the zip file
                                    using (var compressedPageStream = new MemoryStream())
                                    {
                                        var reader = new PdfReader(pageStream.ToArray());
                                        using (var stamper = new PdfStamper(reader, compressedPageStream))
                                        {
                                            stamper.Writer.SetFullCompression();
                                            stamper.Writer.CompressionLevel = PdfStream.BEST_COMPRESSION;
                                        }

                                        var compressedPageBytes = compressedPageStream.ToArray();

                                        // Write the compressed page to the zip file
                                        var entry = new ZipEntry($"{inputFileName}-page-{i}.pdf");
                                        zipOutputStream.PutNextEntry(entry);
                                        zipOutputStream.Write(compressedPageBytes, 0, compressedPageBytes.Length);
                                        zipOutputStream.CloseEntry();
                                    }
                                }
                            }

                            zipOutputStream.Finish();
                            zipOutputStream.Close();
                        }

                        var resultBytes = memoryStream.ToArray();
                        return File(resultBytes, "application/zip", $"{inputFileName}.zip");
                    }
                }
            }
        }
    }
}
