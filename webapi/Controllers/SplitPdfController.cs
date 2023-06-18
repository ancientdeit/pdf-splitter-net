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
using PdfDictionary = iText.Kernel.Pdf.PdfDictionary;
using PdfName = iText.Kernel.Pdf.PdfName;
using PdfStream = iText.Kernel.Pdf.PdfStream;
using PdfDocument = iText.Kernel.Pdf.PdfDocument;
using PdfReader = iText.Kernel.Pdf.PdfReader;

namespace PdfSplitter.Controllers
{
    [Route("api/[controller]")]
    public class UploadController : Controller
    {
        private static Image ConvertToBlackAndWhitePng(PdfImageXObject image)
        {

            /* We want to convert image to grayscale. In PDF this corresponds
             * to DeviceGray color space. Images in DeviceGray colorspace shall have
             * only 8 bits per pixel (bpp).
             * In C# 8bpp images are not well supported, therefore we would need to perform
             * some tricks on a low level in order to convert RGB 24bpp image to 8 bit image.
             *
             * We will manually set image pixel 8 bit values according to original image
             * RGB pixel values. We know from PDF specification, that DeviceGray color space
             * treats each pixel as the value from 0 to 255 and we know that taking an average
             * of RGB values is a very basic but working approach to get corresponding grayscale
             * value.
             * Note that due to C# restrictions we create image with indexed colorspace
             * (Format8bppIndexed). For now we don't care what are the actual colors in color
             * palette, because we already define pixle values as if they were in grayscale
             * color space. We will explicitly overide color space directly in PDF object later.
             */
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

        public static void CompressImagesOnPdf(PdfDocument input)
        {
            // Assume that there is a single XObject in the source document
            // and this single object is an image.
            PdfDictionary pageDict = input.GetFirstPage().GetPdfObject();
            PdfDictionary resources = pageDict.GetAsDictionary(PdfName.Resources);
            PdfDictionary xObjects = resources.GetAsDictionary(PdfName.XObject);

            if (xObjects == null || xObjects.Size() == 0)
            {
                return;
            }

            PdfName imgRef = xObjects.KeySet().First();
            PdfStream pdfStream = xObjects.GetAsStream(imgRef);
            PdfImageXObject imageXObject = new PdfImageXObject(pdfStream);

            using (MemoryStream memoryStreamImage = new MemoryStream(imageXObject.GetImageBytes()))
            {
                Bitmap bitmap = new Bitmap(memoryStreamImage);

                // Resize the image
                int newWidth = bitmap.Width / 2;  // or whatever width you want
                int newHeight = bitmap.Height / 2;  // or whatever height you want
                Bitmap resizedBitmap = ResizeImage(bitmap, newWidth, newHeight);

                // Compress the image and save it to a MemoryStream
                using (MemoryStream compressedStream = new MemoryStream())
                {
                    ImageCodecInfo codecInfo = GetEncoderInfo("image/jpeg");
                    EncoderParameters encoderParameters = new EncoderParameters(1);
                    Encoder myEncoder = Encoder.Quality;
                    encoderParameters.Param[0] = new EncoderParameter(myEncoder, 10L);
                    resizedBitmap.Save(compressedStream, codecInfo, encoderParameters);

                    // Replace the image in the PDF
                    ImageData compressedImageData = ImageDataFactory.Create(compressedStream.ToArray());
                    PdfImageXObject compressedImageXObject = new PdfImageXObject(compressedImageData);
                    xObjects.Put(imgRef, compressedImageXObject.GetPdfObject());
                }
            }
        }

        public static System.Drawing.Image ConvertPdfPageToImage(Stream pdfStream, int pageNumber)
        {
            pdfStream.Position = 0;
            using (var document = PdfiumViewer.PdfDocument.Load(pdfStream))
            {
                return document.Render(pageNumber, 300, 300, true);
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

                                        using (var writer = new iText.Kernel.Pdf.PdfWriter(pageStream, writerProperties))
                                        {
                                            using (var pdfCopy = new PdfDocument(writer))
                                            {
                                                pdfDocument.CopyPagesTo(i, i, pdfCopy);
                                                CompressImagesOnPdf(pdfCopy);
                                                pdfCopy.Close();
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
