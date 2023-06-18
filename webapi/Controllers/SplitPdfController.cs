using iText.Kernel.Pdf;
using Microsoft.AspNetCore.Mvc;
using ICSharpCode.SharpZipLib.Zip;
using iText.IO.Image;
using iText.Kernel.Pdf.Xobject;
using Image = iText.Layout.Element.Image;
using System.Drawing.Imaging;
using System.Drawing;
using System.Runtime.InteropServices;
using PdfiumViewer;
using iText.Layout;
using iText.Layout.Element;

using PdfDictionary = iText.Kernel.Pdf.PdfDictionary;
using PdfName = iText.Kernel.Pdf.PdfName;
using PdfStream = iText.Kernel.Pdf.PdfStream;
using PdfDocument = iText.Kernel.Pdf.PdfDocument;
using PdfReader = iText.Kernel.Pdf.PdfReader;
using System.Globalization;

namespace PdfSplitter.Controllers
{
    [Route("api/[controller]")]
    public class UploadController : Controller
    {
        private static Image ConvertToBlackAndWhitePng(PdfImageXObject image)
        {
            MemoryStream memoryStream = new MemoryStream(image.GetImageBytes());
            Bitmap original = new Bitmap(memoryStream);
            memoryStream.Close();
            Bitmap result = new Bitmap(original.Width, original.Height, PixelFormat.Format8bppIndexed);

            BitmapData data = result.LockBits(new System.Drawing.Rectangle(0, 0, result.Width, result.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            byte[] bytes = new byte[data.Height * data.Stride];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

            // Convert all pixels to grayscale
            for (int y = 0; y < original.Height; y++)
            {
                for (int x = 0; x < original.Width; x++)
                {
                    var c = original.GetPixel(x, y);
                    var rgb = (byte)((c.R + c.G + c.B) / 3);
                    bytes[y * data.Stride + x] = rgb;
                }
            }

            Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
            result.UnlockBits(data);

            using (var stream = new MemoryStream())
            {
                result.Save(stream, ImageFormat.Png);

                /* As discussed above, we want this image to have /DeviceGray colorspace and we have already
                 * ensured that image pixel values are defined correctly for this color space on low level
                 * (by taking average values of red, green and blue components). So, we explicitly override
                 * color space directly in the created image PDF object.
                 */
                ImageData imageData = ImageDataFactory.Create(stream.ToArray());
                PdfImageXObject imageXObject = new PdfImageXObject(imageData);
                PdfStream imageXObjectStream = imageXObject.GetPdfObject();
                imageXObjectStream.Put(PdfName.ColorSpace, PdfName.DeviceGray);

                // Remove a redundant submask
                imageXObjectStream.Remove(PdfName.Mask);

                /* In C# an alpha channel (transparency) is automatically added, however we know that original
                 * image didn't have transparency, that's why we just explicitly throw away any transparency
                 * defined for the PDF object that represents an image.
                 */
                return new Image(imageXObject);
            }
        }

        public static Bitmap ResizeImage(Bitmap originalImage, int newWidth, int newHeight)
        {
            var resizedImage = new Bitmap(newWidth, newHeight);

            using (var graphics = Graphics.FromImage(resizedImage))
            {
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;

                graphics.DrawImage(originalImage, 0, 0, newWidth, newHeight);
            }

            return resizedImage;
        }

        public static System.Drawing.Image ConvertPdfPageToImage(byte[] pdfBytes, int pageNumber, float dpi)
        {
            using (var pdfStream = new MemoryStream(pdfBytes))
            {
                pdfStream.Position = 0;
                using (var document = PdfiumViewer.PdfDocument.Load(pdfStream))
                {
                    return document.Render(pageNumber - 1, dpi, dpi, true);
                }
            }
        }

        // Helper method to get the encoder for a given MIME type
        public static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            int j;
            ImageCodecInfo[] encoders = ImageCodecInfo.GetImageEncoders();
            for (j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }

        public static void CompressAndConvertImageToPdf(System.Drawing.Image image, PdfWriter writer, float scale)
        {
            // Resize the image
            var bitmap = new Bitmap(image);
            int newWidth = (int)(bitmap.Width * scale);  // or whatever width you want
            int newHeight = (int)(bitmap.Height * scale);  // or whatever height you want
            Bitmap resizedBitmap = ResizeImage(bitmap, newWidth, newHeight);

            // Convert Bitmap to byte array
            ImageConverter converter = new ImageConverter();
            byte[] imageBytes = (byte[])converter.ConvertTo(resizedBitmap, typeof(byte[]));

            // Create a PDF document
            using (var pdfDocument = new PdfDocument(writer))
            {
                var document = new Document(pdfDocument);

                // Create an ImageData object
                var imageData = ImageDataFactory.Create(imageBytes);

                // Create an Image object and add it to the document
                var pdfImg = new Image(imageData);
                document.Add(pdfImg);

                document.Close();
            }
        }

        [HttpPost]
        public IActionResult Upload(IFormFile file, [FromForm] string scale, [FromForm] string compression, [FromForm] string dpi)
        {
            float scaleValue;
            float dpiValue;
            int compressionValue;

            if (float.TryParse(scale, NumberStyles.Float, CultureInfo.InvariantCulture, out scaleValue))
            {
                // The string was successfully converted to a float, you can now use `scaleValue` in your code
            }
            else
            {
                // The string could not be converted to a float, you might want to return an error response
                return BadRequest("Invalid scale value provided");
            }

            if (int.TryParse(compression, NumberStyles.Integer, CultureInfo.InvariantCulture, out compressionValue))
            {
                // The string was successfully converted to a integer, you can now use `compressionValue` in your code
            }
            else
            {
                // The string could not be converted to a integer, you might want to return an error response
                return BadRequest("Invalid compression value provided");
            }

            if (float.TryParse(dpi, NumberStyles.Float, CultureInfo.InvariantCulture, out dpiValue))
            {
                // The string was successfully converted to a float, you can now use `compressionValue` in your code
            }
            else
            {
                // The string could not be converted to a float, you might want to return an error response
                return BadRequest("Invalid compression value provided");
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            string inputFileName = Path.GetFileNameWithoutExtension(file.FileName);

            using (var memoryStream = new MemoryStream())
            {
                file.CopyTo(memoryStream);
                memoryStream.Position = 0;

                var pdfBytes = memoryStream.ToArray();

                using (var pdfReader = new PdfReader(new MemoryStream(pdfBytes)))
                {
                    using (var pdfDocument = new PdfDocument(pdfReader))
                    {
                        using (var zipStream = new MemoryStream())
                        {
                            using (var zipOutputStream = new ZipOutputStream(zipStream))
                            {
                                for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
                                {
                                    var image = ConvertPdfPageToImage(pdfBytes, i, dpiValue);

                                    using (var pageStream = new MemoryStream())
                                    {
                                        var writerProperties = new WriterProperties();
                                        writerProperties.SetPdfVersion(PdfVersion.PDF_2_0);
                                        writerProperties.SetCompressionLevel(compressionValue);

                                        using (var writer = new iText.Kernel.Pdf.PdfWriter(pageStream, writerProperties))
                                        {
                                            CompressAndConvertImageToPdf(image, writer, scaleValue);
                                        }

                                        var compressedPageBytes = pageStream.ToArray();

                                        // Write the page to the zip file
                                        var entry = new ZipEntry($"{inputFileName}-page-{i}.pdf");
                                        zipOutputStream.PutNextEntry(entry);
                                        zipOutputStream.Write(compressedPageBytes, 0, compressedPageBytes.Length);
                                        zipOutputStream.CloseEntry();
                                    }
                                }

                                zipOutputStream.Finish();
                                zipOutputStream.Close();

                                var resultBytes = zipStream.ToArray();
                                return File(resultBytes, "application/zip", $"{inputFileName}.zip");
                            }
                        }
                    }
                }
            }
        }
    }
}