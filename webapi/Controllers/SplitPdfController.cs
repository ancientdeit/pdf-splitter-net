using System.IO;
using iText.Kernel.Pdf;
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
                    using (var pdfDocument = new PdfDocument(pdfReader))
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            using (var zipOutputStream = new ZipOutputStream(memoryStream))
                            {
                                for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
                                {
                                    using (var pageStream = new MemoryStream())
                                    {
                                        var writerProperties = new WriterProperties();
                                        writerProperties.SetPdfVersion(PdfVersion.PDF_2_0);
                                        writerProperties.SetCompressionLevel(CompressionConstants.BEST_COMPRESSION);

                                        using (var writer = new PdfWriter(pageStream, writerProperties))
                                        {
                                            using (var pdfCopy = new PdfDocument(writer))
                                            {
                                                pdfDocument.CopyPagesTo(i, i, pdfCopy);
                                            }
                                        }

                                        var pageBytes = pageStream.ToArray();

                                        // Write the page to the zip file
                                        var entry = new ZipEntry($"{inputFileName}-page-{i}.pdf");
                                        zipOutputStream.PutNextEntry(entry);
                                        zipOutputStream.Write(pageBytes, 0, pageBytes.Length);
                                        zipOutputStream.CloseEntry();
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
}
